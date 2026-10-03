using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CommCodeVerifier.Core;

public sealed class ProcessOptions
{
    public HashSet<RuleId> Enabled { get; init; } = new(RuleCatalog.All);
    /// <summary>Шаблон размеров/резьбы должен покрывать токен целиком (защита 40N3X050A0250).</summary>
    public bool RequireWholeToken { get; init; } = true;
    public int MaxEndingIterations { get; init; } = 5;
    /// <summary>Доп. символы, допустимые в кодах для 1С (по ТЗ — пусто: только [0-9A-Z]).</summary>
    public string Extra1CChars { get; init; } = "";
    /// <summary>
    /// Сочетания кириллицы, для которых правило 10 игнорируется.
    /// Разделитель — запятая, сравнение без учёта регистра. Пример: «ТВСР,НН».
    /// Символы внутри совпавшего подстроки не заменяются.
    /// </summary>
    public string CyrillicExceptions { get; init; } = "";
    /// <summary>
    /// Серии номенклатуры (начало кода), для которых правило 8 не применяется,
    /// технические блоки не препятствуют форме «* *», а классификатор правила 12
    /// не используется. Разделитель — запятая. Пример: «H74H» сохраняет код H74H-16P DN50.
    /// </summary>
    public string EndingKeepSeries { get; init; } = "";
    public bool AllowEmptyingByEnding { get; init; } = true;
    /// <summary>
    /// Правило 8 удаляет окончание только если оно было окончанием значения
    /// ДО удаления значащих слов. false — прежнее поведение.
    /// </summary>
    public bool EndingMustBeOriginal { get; init; } = true;
}

public sealed class ProcessResult
{
    public string Article { get; init; } = "";
    public string Original { get; init; } = "";
    public string Result { get; set; } = "";
    /// <summary>Значение столбца «Номенклатура.Группа выгрузки ИМ (Общие)» из исходного файла.</summary>
    public string Group { get; set; } = "";
    public List<RuleId> Applied { get; } = new();
    public HashSet<int> Highlight { get; set; } = new();
    public bool DeletedByArticle { get; set; }
    public bool TrailingResidue { get; set; }
    /// <summary>Правило 7 удалило значащее слово, стоявшее в начале исходного значения.</summary>
    public bool LeadingWordRemoved { get; set; }
    /// <summary>
    /// В результате есть марка стали на кириллице (08Х18Н10). Правило 10 её не конвертирует,
    /// строка идёт в «Коды для ручной проверки» с отдельным критерием, без заливки.
    /// </summary>
    public bool SteelGradeCyrillic { get; set; }
    public bool Suspicious { get; set; }
    public bool ReadyFor1C { get; set; }
    /// <summary>Правило 12: строка с каталогом/моделью после значащего слова отправляется вручную с результатом.</summary>
    public bool CatalogReviewWithResult { get; set; }
    /// <summary>Правило 12: после значащих слов остались только технические параметры/чужой код — результат пустой.</summary>
    public bool CatalogReviewEmpty { get; set; }
    /// <summary>
    /// Правило 12: значение коммерческого кода начинается со слова из списка
    /// безусловного удаления — код удаляется целиком, минуя классификатор.
    /// </summary>
    public bool CatalogReviewForcedEmpty { get; set; }
    /// <summary>
    /// Код начинается с серии из «Серии, для которых не удаляем окончания»:
    /// правило 8 не применялось, технические блоки (DN50, PN16, M6…) не препятствуют
    /// форме «* *», классификатор правила 12 не применялся.
    /// </summary>
    public bool KeepSeries { get; set; }
    /// <summary>Текст исключения, если обработка строки сорвалась. null — обработка прошла.</summary>
    public string? ProcessingError { get; set; }

    public bool IsEmptyResult => Result.Length == 0;
    public bool Changed => !string.Equals(Original, Result, StringComparison.Ordinal);

    public string CriteriaText
    {
        get
        {
            // Сбойная строка не выдаёт себя за успешно обработанную.
            if (ProcessingError != null)
                return "ОШИБКА ОБРАБОТКИ: " + ProcessingError;

            var parts = Applied.Select(RuleCatalog.CriteriaName).ToList();
            if (SteelGradeCyrillic) parts.Add(CodeProcessor.SteelGradeCriteria);
            return string.Join(" + ", parts);
        }
    }
}

public static class CodeProcessor
{
    // ---------- единицы измерения ----------
    private static readonly string[] Units =
    {
        "об/мин","л/мин","м3/ч","кОм","МОм","кВт","кПа","МПа","мкм","мм2","мм²","кГц","МГц",
        "kOhm","MOhm","mm2","бар","bar","дюйм","inch","psi","rpm","кг","мм","см","мл","мг","дБ","dB",
        "Гц","Hz","Вт","кВ","мВ","мА","шт","pcs","мин","сек","min","sec","Нм","Nm","Ом","Па",
        "MPa","kPa","mm","cm","kg","ml","mg","in","ft","°C","Pa","В","А","м","г","л","ч","с","V","A","W","K","К"
    };
    private static readonly string UnitAlt =
        "(?:" + string.Join("|", Units.OrderByDescending(u => u.Length).Select(Regex.Escape)) + ")";

    // ---------- шаблоны правил ----------
    // Правило 5: обозначение резьбы. «M» в начале токена или после разделителя
    // (пробел, - / ( [ , ; =), поэтому распознаются и «-М5-М», и «-М0,75х0,25-F».
    // Диаметр начинается с 0 только как «0,» или «0.»: резьбы М00…/М004 не бывает,
    // поэтому «-М004-» в кодах вида КЭМ-М004-РФ01 резьбой не считается.
    private static readonly Regex ThreadRx = new(
        @"(?:^|(?<=[\s\-/(\[,;=]))([MМmм])\s?(0[.,]\d{1,3}|[1-9]\d{0,3}(?:[.,]\d{1,3})?)" +
        @"(?:\s?([xXхХ])\s?(\d{1,4}(?:[.,]\d{1,3})?))?" +
        @"(?=$|[\s\-/)\],;=]|мм|mm)", RegexOptions.Compiled);

    private static readonly Regex ThreadStartRx = new(@"^[MМmм]\s?\d", RegexOptions.Compiled);

    // Правило 4: перечисление размеров — ТОЛЬКО как целый токен (ответ на вопрос 2 ТЗ).
    private static readonly Regex DimTokenRx = new(
        @"^[Øø⌀Φ]?\d{1,5}(?:[.,]\d{1,3})?(?:\s?[xXхХ]\s?\d{1,5}(?:[.,]\d{1,3})?){1,5}" +
        "(?:" + UnitAlt + "|\")?$", RegexOptions.Compiled);

    private static readonly Regex DottedNumberRx = new(@"^\d+(?:\.\d+){2,}$", RegexOptions.Compiled);

    /// <summary>
    /// Цепочка групп через точку: 2 и более точек между группами букв/цифр
    /// (вида ***.***.***): 28.58.49, 302.1302A.01, AB.12.C3. Это обозначение/артикул,
    /// а не десятичные числа — точки в такой цепочке правилом 1 не заменяются.
    /// </summary>
    private static readonly Regex DottedChainRx = new(@"[\p{L}\d]+(?:\.[\p{L}\d]+){2,}", RegexOptions.Compiled);
    private static readonly Regex PureDecimalRx = new(@"^\d{1,6}\.\d{1,4}$", RegexOptions.Compiled);
    private static readonly Regex TrailDecimalRx = new(@"\d{1,6}\.\d{1,4}$", RegexOptions.Compiled);
    private static readonly Regex UnitTokenRx = new("^" + UnitAlt + "$", RegexOptions.Compiled);
    private static readonly Regex NumUnitInTokenRx = new(
        @"\d{1,6}\.\d{1,4}(?=\s?" + UnitAlt + @"(?![\p{L}\d]))", RegexOptions.Compiled);

    // ---------- марки стали (ГОСТ 4543, 5632, 19281, 380, 801, 19265) ----------
    public const string SteelGradeCriteria = "Марка стали в коде - кириллица";

    /// <summary>Буквенные обозначения легирующих элементов (+ А — азот/высококачественная).</summary>
    private const string SteelElements = "ХНМТГФСВЮКРДБАЦЕП";

    /// <summary>Слова, указывающие на контекст материала.</summary>
    private static readonly HashSet<string> MaterialWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ШАРИК", "ПОДШИПНИК", "СТАЛЬ", "ПРУТОК", "ЛИСТ", "ПОЛОСА", "КРУГ",
        "ШЕСТИГРАННИК", "ТРУБА", "ПРОВОЛОКА", "ПОКОВКА", "ОТЛИВКА", "МЕТАЛЛ", "ГОСТ"
    };

    /// <summary>Единицы измерения (допустимые суффиксы после числа).</summary>
    private static readonly Regex UnitSuffixRx = new(
        @"^(?:МКФ|МФ|НФ|ПФ|КМ|ММ|СМ|ДМ|МКМ|КВТ|МВТ|ГВТ|ВТ|КВ|МВ|МА|КА|А|В|М|К|КГ|МГ|Г|Т|Л|МЛ|" +
        @"ГЦ|КГЦ|МГЦ|ДБ|ОМ|КОМ|МОМ|БАР|ПА|КПА|МПА|ШТ|МИН|СЕК|Ч|НМ|ЕД)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Арматурные/размерные литеры, которые не считаются маркой стали.</summary>
    private static readonly HashSet<char> ExcludedPrefixes = new() { 'М', 'К', 'П', 'Г', 'С', 'Ф', 'В', 'Ш', 'Л', 'Д', 'Н' };

    /// <summary>
    /// Strong: многоэлементная марка стали (минимум 2 группы "элемент+цифры").
    /// Пример: 08Х18Н10, 12Х18Н10Т, 40ХН2МА, 09Г2С.
    /// </summary>
    private static readonly Regex StrongMultiRx = new(
        @"^\d{1,3}(?:[" + SteelElements + @"]\d{0,2}){2,}(?:пс|сп|кп|-?Ш|-?ВД)?$",
        RegexOptions.Compiled);

    /// <summary>Специальные формы марок: ШХ/ЭХ.</summary>
    private static readonly Regex SpecialSHXRx = new(
        @"^(?:ШХ|ЭХ)\d{1,2}(?:[" + SteelElements + @"]\d{0,2})*(?:пс|сп|кп|-?Ш|-?ВД)?$",
        RegexOptions.Compiled);

    /// <summary>Специальные формы марок: быстрорежущая Р.</summary>
    private static readonly Regex SpecialRRx = new(
        @"^Р\d{1,2}(?:[" + SteelElements + @"]\d{0,2}){1,5}(?:пс|сп|кп|-?Ш|-?ВД)?$",
        RegexOptions.Compiled);

    /// <summary>Специальные формы марок: Ст (обычная углеродистая).</summary>
    private static readonly Regex SpecialStRx = new(
        @"^Ст\d{1,2}(?:[" + SteelElements + @"]\d{0,2}){0,3}(?:пс|сп|кп)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Классифицирует токен как марку стали.
    /// Strong — уверенная марка (многоэлементная структура), кириллица сохраняется.
    /// None — единица измерения, размер, техническое обозначение или пограничная форма.
    /// </summary>
    public static bool IsCyrillicSteelGrade(string token, string fullValue)
    {
        string core = token.Trim(EdgeTrim);
        if (core.Length == 0 || !core.Any(CharReplacements.IsCyrillic))
            return false;

        // Исключаем чистые числа и размеры вида 5x20, 10×8
        if (Regex.IsMatch(core, @"^\d+$") || Regex.IsMatch(core, @"^\d+[xхX×*/]\d+"))
            return false;

        // Исключаем единицы измерения: число + единица (24В, 125А, 500М, 220МКФ)
        var m = Regex.Match(core, @"^\d+([А-Яа-яЁё]+)$");
        if (m.Success)
        {
            string suf = m.Groups[1].Value.ToUpper();
            if (UnitSuffixRx.IsMatch(suf))
                return false;
            // Если суффикс длиной >= 2 и не входит в список раскисления, считаем единицей
            if (suf.Length >= 2 && suf != "ПС" && suf != "СП" && suf != "КП" && suf != "Ш" && suf != "ВД")
                return false;
        }

        // Исключаем арматурные/размерные литеры: М16, П2, Ф6, К2
        if (core.Length >= 2 && char.IsLetter(core[0]) && ExcludedPrefixes.Contains(char.ToUpper(core[0])))
            return false;

        // Проверяем контекст материала
        bool hasMaterial = MaterialWords.Any(w => fullValue.Contains(w, StringComparison.OrdinalIgnoreCase));

        // Strong: многоэлементная марка
        if (StrongMultiRx.IsMatch(core))
            return true;

        // Strong: ШХ15, ЭХ12 (при контексте материала или развёрнутой структуре)
        if (SpecialSHXRx.IsMatch(core))
            return hasMaterial || core.Length >= 4;

        // Strong: Р6М5 (при контексте материала или развёрнутой структуре)
        if (SpecialRRx.IsMatch(core))
            return hasMaterial || core.Length >= 4;

        // Strong: Ст20 (при контексте материала)
        if (SpecialStRx.IsMatch(core))
            return hasMaterial;

        // Остальное — None (пограничные формы П2, Ф6, 40Х, 1Р и т.д.)
        return false;
    }

    /// <summary>Токен — марка стали, записанная кириллицей (для обратной совместимости).</summary>
    public static bool IsCyrillicSteelGrade(string token)
    {
        return IsCyrillicSteelGrade(token, token);
    }


    // ---------- технические обозначения и служебные слова ----------

    /// <summary>
    /// Маркер каталожного/модельного обозначения в исходном значении:
    /// «каталог», «серия», «модель», «тип», «арт.» и латинские аналоги.
    /// </summary>
    /// <summary>
    /// Обозначение стандарта: DIN, ISO, ГОСТ, EN, ANSI и т.п. с номером
    /// (DIN988, ГОСТ17376, ISO7380-1) или без него (DIN, ГОСТ).
    /// Наличие такого токена означает техническое обозначение изделия, а не код.
    /// Ограничение до двух числовых групп плюс необязательная хвостовая буква:
    /// так ловятся DIN988, ГОСТ17475-80, ISO7380-1 и DIN7993-B,
    /// но не каталожные коды вида EN354-015-33 (три группы — уже не стандарт).
    /// </summary>
    private static readonly Regex StandardRx = new(
        @"^(?:DIN|ISO|GOST|ANSI|JIS|EN|BS|UNI|NF|SAE|ASTM|\u0413\u041e\u0421\u0422)(?:[-\u2013]?\d+){0,2}(?:[-\u2013][A-Z])?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CatalogMarkerRx = new(
        @"(?:^|\s)(?:каталог|кат\.?|серия|сер\.?|модель|мод\.?|тип|исполнение|арт\.?|" +
        @"catalog|cat\.?|series|model|type|art\.?)(?:\s|:|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Каталожный номер в остатке значения: сочетание цифр и букв с разделителями,
    /// характерное для обозначений вида 6ES7-315, CJX2-1210, AL-302.1302A.01.
    /// </summary>
    private static readonly Regex CatalogNumberRx = new(
        @"^(?=[^\s]*\d)(?=[^\s]*[A-Za-z\u0410-\u042f\u0401])[A-Z0-9\u0410-\u042f\u0401][A-Z0-9\u0410-\u042f\u0401x\-.,/]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Код, в котором помимо букв и цифр допустимы технические разделители
    /// (дефис, точка, слэш, знак умножения) — но не буквы вне [A-Z] и не кириллица.
    /// </summary>
    private static readonly Regex CodeWithTechnicalRx = new(
        @"^(?=[^\s]*\d)[A-Z0-9][A-Z0-9xX\-.,/\u00d7\u0445]*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Токен — техническое обозначение, а не каталожный/модельный код:
    /// класс прочности, стандарт, условный проход, давление, обозначение резьбы, единица измерения.
    /// </summary>
    private static readonly Regex TechnicalTokenRx = new(
        @"^(?:" +
            @"(?:A[1-5]|C[1-4]|F1)(?:[-\u2013]?\d{2})?" +                 // A2, A4, A2-70, C3-80
            @"|(?:3|4|5|6|8|9|10|12)[.,]\d" +                              // 8.8, 10.9, 4.8
            @"|(?:DIN|ISO|GOST|ANSI|JIS|EN|BS|UNI|NF|SAE|ASTM|\u0413\u041e\u0421\u0422)[-\u2013]?\d*" +
            @"|(?:DN|PN|NW|\u0414\u0443)[-\u2013]?\d*" +                // DN50, PN16, Ду50
            @"|M\d{1,3}(?:[X\u0425x]\d{1,3}(?:[.,]\d{1,3})?)?" +        // M6, M6X20, M5X0,80
            @"|AISI\d{3}[A-Z]{0,2}" +                                      // AISI316, AISI316L, AISI420
            @"|\d[A-Z]{2}" +                                               // 1SN, 2SN, 4SH, 4SP (рукава EN 853/856)
            @"|(?:BSP|NPT|BSPT|Rp|RC|G)\d[\d/]*" +                        // BSP3/4, G1/2, Rp1
            @"|\u0413\u041e\u0421\u0422\d+" +                           // ГОСТ17376, ГОСТ15180 (слитно)
            @"|\d+(?:[.,]\d+)?(?:[xX\u0445\u0425\u00d7]\d+(?:[.,]\d+)?)+(?:-\d+(?:[.,]\d+)?)?" +  // 88x6x76, 12X20-1,6, 8,5x1,5
            @"|\d+/\d+" +                                                 // 1/2, 3/8, 11/4 (трубные дроби)
            @"|\d+(?:[.,]\d+)?(?:" + UnitAlt + @")?" +                    // 10, 1,5mm, 220В
        @")$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Служебные слова, не несущие смысла в коде: предлоги, союзы, частицы
    /// и их английские аналоги. Удаляются, если остались без значащих слов вокруг.
    /// </summary>
    private static readonly HashSet<string> OrphanedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "и", "в", "во", "не", "что", "он", "на", "я", "с", "со", "как", "а", "то", "все",
        "она", "так", "его", "но", "да", "ты", "к", "ко", "у", "же", "вы", "за", "бы", "по",
        "только", "ее", "мне", "было", "вот", "от", "меня", "еще", "нет", "о", "об", "из",
        "ему", "теперь", "когда", "даже", "ну", "вдруг", "ли", "если", "уже", "или", "ни",
        "быть", "был", "него", "до", "вас", "нибудь", "опять", "уж", "вам", "ведь", "там",
        "потом", "себя", "ничего", "ей", "может", "они", "тут", "где", "есть", "надо", "ней",
        "для", "мы", "тебя", "их", "чем", "была", "сам", "чтоб", "без", "будто", "чего", "раз",
        "тоже", "себе", "под", "будет", "ж", "тогда", "кто", "этот", "того", "потому", "этого",
        "какой", "совсем", "ним", "здесь", "этом", "один", "мой", "тем", "чтобы",
        "нее", "сейчас", "были", "куда", "зачем", "сказать", "всех", "никогда", "сегодня",
        "можно", "при", "наконец", "два", "об", "другой", "хоть", "после", "над", "больше",
        "тот", "через", "эти", "нас", "про", "всего", "них", "какая", "много", "разве",
        "три", "эту", "моя", "впрочем", "хорошо", "свою", "этой", "перед", "иногда", "лучше",
        "чуть", "том", "нельзя", "такой", "им", "более", "всегда", "конечно", "всю", "между",
        // английские предлоги и союзы
        "and", "with", "for", "of", "per", "the", "a", "an", "to", "in", "on", "by", "or",
        "from", "at", "as", "is", "are", "be", "no", "not"
    };
    // ---------- правило 12: зависимая конструкция в родительном падеже ----------
    // «Крышка насоса», «Ремкомплект распределителя», «Шток поршня»: позиция связана
    // с другим изделием — код после такой конструкции относится к нему, а не к позиции.

    /// Прилагательные в им. п. и жен. род. п. («-ой», «-ей» у «тонкой», «верхней»
    /// не учитываются: «-ей» у существительных — род. п. мн. ч., см. NounGenitiveEndings).
    private static readonly string[] AdjSkipEndings =
        { "ого", "его", "ых", "их", "ый", "ий", "ой", "ая", "яя", "ое", "ее", "ые", "ие" };

    /// Окончания существительных в род. п. ед. и мн. ч.
    private static readonly string[] NounGenitiveEndings =
        { "ов", "ев", "ёв", "ей", "а", "я", "ы", "и" };

    private static readonly Regex CyrWordRx = new(
        @"^[А-Яа-яЁё]+(?:-[А-Яа-яЁё]+)*$", RegexOptions.Compiled);

    private const int GenitiveScanLimit = 4;
        /// Слова-«наборы»: следующее за ними слово — содержимое набора
    /// («Комплект прокладок», «Набор ключей»), а не связь с другим изделием.
    /// Ремкомплект сюда не входит: «Ремкомплект распределителя» — связь с изделием.
    private static readonly HashSet<string> CollectiveHeads = new(StringComparer.OrdinalIgnoreCase)
    {
        "комплект", "комплекты", "набор", "наборы",
        "упаковка", "упаковки", "пачка", "пачки", "партия"
    };
    private static readonly Regex CodeLikeRx = new(@"^[0-9A-Z][0-9A-Zx\-.,/°""']*$", RegexOptions.Compiled);
    private static readonly Regex SuspiciousRx = new(
        @"(\b(DIN|ISO|GOST|ANSI|JIS|EN)\s?\d)|(\bГОСТ)|(\bDN\s?\d)|(\bДу\s?\d)|(^|\s)M\d+X\d|" +
        @"(\s(A2|A4|A5|8[.,]8|10[.,]9|12[.,]9|4[.,]8|5[.,]8)(\s|$))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ---------- форма «* *» ----------
    /// <summary>Кэш шаблонов формы «* *» по строке доп. символов.</summary>
    private static readonly ConcurrentDictionary<string, Regex> TwoBlockCache = new();

    /// <summary>
    /// Шаблон «* *» для заданного набора доп. символов: два блока, между ними
    /// строго один пробел. Пробельные символы из набора исключаются — иначе
    /// блоки перестали бы различаться и «A B C» совпало бы как два блока.
    /// </summary>
    private static Regex GetTwoBlockRx(string? extra)
    {
        return TwoBlockCache.GetOrAdd(extra ?? "", key =>
        {
            var cls = new StringBuilder("0-9A-Z");
            foreach (char c in key)
            {
                if (char.IsWhiteSpace(c)) continue;

                // Экранирование через \uXXXX: безопасно внутри символьного класса
                // для любого символа, включая «]», «^», «-» и «\».
                cls.Append($"\\u{(int)c:X4}");
            }

            string block = "([" + cls + "]+)";
            return new Regex("^" + block + " " + block + "$", RegexOptions.Compiled);
        });
    }

    /// <summary>
    /// Блок, который не может быть частью комм. кода: класс прочности метиза,
    /// обозначение стандарта, условный проход или обозначение резьбы.
    /// Набор учитывает, что доп. символы делают возможными составные
    /// обозначения вида «A2-70», «8,8», «M5X0,80».
    /// </summary>
    private static readonly Regex NonCodeBlockRx = new(
        @"^(?:" +
            // классы нержавеющих, в т.ч. составные: A2, A4, A2-70, C3-80
            @"(?:A[1-5]|C[1-4]|F1)(?:[-–]?\d{2})?" +
            // классы прочности углеродистых: 8.8, 8,8, 10.9, 4.8 …
            @"|(?:3|4|5|6|8|9|10|12)[.,]\d" +
            // обозначения стандартов, в т.ч. с дефисом: DIN913, DIN-913, ISO4762
            @"|(?:DIN|ISO|GOST|ANSI|JIS|EN|BS|UNI|NF|SAE|ASTM)[-–]?\d*" +
            // условный проход и давление: DN50, PN16, DN-50
            @"|(?:DN|PN|NW)[-–]?\d*" +
            // обозначение резьбы, в т.ч. с дробным шагом: M6, M6X20, M5X0,80
            @"|M\d{1,3}(?:[XХ]\d{1,3}(?:[.,]\d{1,3})?)?" +
        @")$", RegexOptions.Compiled);

    /// <summary>
    /// Значение имеет вид «* *» и ни один из блоков не является техническим
    /// обозначением. Состав допустимых символов зависит от настройки
    /// «Доп. символы, допустимые в файле для 1С».
    /// allowTechnicalBlocks = true — для кодов из списка серий
    /// («H74H-16P DN50»): технические блоки не препятствуют форме «* *».
    /// </summary>
    private static bool IsAcceptableTwoBlock(string value, string? extra, bool allowTechnicalBlocks = false)
    {
        var m = GetTwoBlockRx(extra).Match(value);
        if (!m.Success) return false;
        if (allowTechnicalBlocks) return true;

        return !NonCodeBlockRx.IsMatch(m.Groups[1].Value)
            && !NonCodeBlockRx.IsMatch(m.Groups[2].Value);
    }

    // ---------- наборы символов ----------
    private static readonly char[] EdgeTrim = ",;:.!?()[]{}\"'«»".ToCharArray();
    private static readonly char[] DashSources = { '\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\u2015', '\u2212', '\uFE58', '\uFE63', '\uFF0D' };
    private static readonly char[] NbspSources = { '\u00A0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u202F', '\u205F', '\u3000' };
    private static readonly char[] ZeroWidth = { '\u200B', '\u200C', '\u200D', '\u2060', '\uFEFF', '\u00AD', '\u180E' };

    // =====================================================================
    public static ProcessResult Process(string? article, string? code, ProcessOptions opts)
    {
        var cfg = ConfigRepository.Instance;
        var res = new ProcessResult { Article = article ?? "", Original = code ?? "" };

        // ---- Правило 9 первым: ранний выход ----
        if (opts.Enabled.Contains(RuleId.ArticleExclusion) && cfg.IsExcludedArticle(res.Article))
        {
            res.Applied.Add(RuleId.ArticleExclusion);
            res.DeletedByArticle = true;
            res.Result = "";
            res.Highlight = new HashSet<int>(Enumerable.Range(0, res.Original.Length));
            res.ReadyFor1C = false;
            return res;
        }

        var t = new TrackedString(res.Original);
        var applied = new HashSet<RuleId>();

        if (opts.Enabled.Contains(RuleId.ControlChars) && ApplyControlChars(t)) applied.Add(RuleId.ControlChars);
        if (opts.Enabled.Contains(RuleId.Dashes) && ApplyDashes(t)) applied.Add(RuleId.Dashes);
        if (opts.Enabled.Contains(RuleId.TrimSpaces) && ApplySpaces(t)) applied.Add(RuleId.TrimSpaces);
        if (opts.Enabled.Contains(RuleId.ThreadDesignation) && ApplyThread(t)) applied.Add(RuleId.ThreadDesignation);
        if (opts.Enabled.Contains(RuleId.DimensionX) && ApplyDimensionX(t, opts)) applied.Add(RuleId.DimensionX);
        if (opts.Enabled.Contains(RuleId.DecimalSeparator) && ApplyDecimal(t)) applied.Add(RuleId.DecimalSeparator);
        // Опорное значение для правила 8: состояние ДО удаления значащих слов.
        string preWords = t.Value;
        // Правило 12: признак «начиналось со значащего слова» фиксируем ЗДЕСЬ,
        // до правила 7, пока первое значащее слово ещё присутствует в значении.
        // После удаления значащих слов определить это по результату уже нельзя.
        bool startsWithSignificantWord = StartsWithSignificantWord(preWords, cfg);

        if (opts.Enabled.Contains(RuleId.SignificantWords) && ApplyWords(t, cfg, out bool leadingWord))
        {
            applied.Add(RuleId.SignificantWords);
            res.LeadingWordRemoved = leadingWord;
            // Убираем «осиротевшие» служебные слова (предлоги, союзы), оставшиеся после удаления значащих.
            RemoveOrphanedWords(t);
        }
        // Серия из списка — окончания не удаляем (проверка до правила 8, после правил 3,6,2,5,4,1,7).
        res.KeepSeries = StartsWithKeepSeries(t.Value, opts.EndingKeepSeries, cfg.CharMap);
        if (opts.Enabled.Contains(RuleId.TrailingEnding) && !res.KeepSeries &&
            ApplyEndings(t, cfg, opts, preWords)) applied.Add(RuleId.TrailingEnding);
        if (opts.Enabled.Contains(RuleId.HomoglyphLetters) && ApplyHomoglyphs(t, cfg.CharMap, opts.CyrillicExceptions ?? "")) applied.Add(RuleId.HomoglyphLetters);
        if (opts.Enabled.Contains(RuleId.TrimSpaces) && ApplySpaces(t)) applied.Add(RuleId.TrimSpaces);

        // Правило 11 — строго после правила 9: при раннем выходе выше оно не выполняется.
        if (opts.Enabled.Contains(RuleId.ArticleExtraction) && ApplyArticleExtraction(t, res.Article))
            applied.Add(RuleId.ArticleExtraction);

        res.Result = t.Value;
        if (opts.Enabled.Contains(RuleId.CatalogStartReview))
        {
            // Безусловное удаление: первое слово значения ДО удаления значащих слов
            // входит в «Слова удаления кода.txt». От списка значащих слов не зависит.
            if (cfg.IsForceDeleteWord(FirstToken(preWords)))
            {
                res.CatalogReviewForcedEmpty = true;
                res.CatalogReviewEmpty = true;
                applied.Add(RuleId.CatalogStartReview);
            }
            else
            {
                var review = ClassifyCatalogStart(res.Original, res.Result, cfg,
                    startsWithSignificantWord, res.KeepSeries);
                res.CatalogReviewWithResult = review == CatalogReviewKind.WithResult;
                res.CatalogReviewEmpty = review == CatalogReviewKind.Empty;
                if (review != CatalogReviewKind.None) applied.Add(RuleId.CatalogStartReview);
            }
        }
        res.Highlight = new HashSet<int>(t.TouchedOriginalIndexes);
        if (res.CatalogReviewEmpty)
        {
            // Код удалён правилом 12: результат пустой, в дубликатах и маркерах не участвует.
            res.Result = "";
            // Как в правиле 9: весь исходный код подсвечивается красным.
            res.Highlight = new HashSet<int>(Enumerable.Range(0, res.Original.Length));
        }
        res.Applied.AddRange(RuleCatalog.DisplayOrder.Where(applied.Contains));

        // ---- маркеры «остатка» (не правила, в «Критерий изменений» не попадают) ----
        bool wordRulesOn = opts.Enabled.Contains(RuleId.SignificantWords) ||
                           opts.Enabled.Contains(RuleId.TrailingEnding);
        var tokens = res.Result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        res.SteelGradeCyrillic = tokens.Any(tok => IsCyrillicSteelGrade(tok, res.Result));
        // Марка стали в конце — не «необработанный остаток», жёлтой заливкой не помечаем.
        if (wordRulesOn && tokens.Length > 1 && !CodeLikeRx.IsMatch(tokens[^1]) &&
            !IsCyrillicSteelGrade(tokens[^1], res.Result))
            res.TrailingResidue = true;

        // Достоверная форма «* *» больше не повод считать результат подозрительным.
        // Форма, отвергнутая из-за технического обозначения в блоке, остаётся
        // подозрительной и получит оранжевую заливку в файле ручной проверки.
        // Для кодов из списка серий технические блоки допустимы.
        if (res.Result.Length > 0 && !IsAcceptableTwoBlock(res.Result, opts.Extra1CChars, res.KeepSeries) &&
            (SuspiciousRx.IsMatch(res.Result) || res.Result.Contains(' ')))
            res.Suspicious = true;

        res.ReadyFor1C = IsReadyFor1C(res.Result, opts.Extra1CChars, res.KeepSeries);
        // Правило 12 всегда отправляет строку в ручную проверку.
        if (res.CatalogReviewWithResult || res.CatalogReviewEmpty) res.ReadyFor1C = false;
        return res;
    }

    public static bool IsReadyFor1C(string value, string extra, bool keepSeries = false)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Форма «* *» проверяется ПЕРВОЙ: посимвольный цикл ниже отвергает пробел.
        if (IsAcceptableTwoBlock(value, extra, keepSeries)) return true;

        foreach (char c in value)
        {
            // Пробел недопустим независимо от настройки: по ТЗ значение идёт
            // «без пробелов между символами», а форма «* *» — единственное
            // санкционированное исключение и проверена выше.
            if (char.IsWhiteSpace(c)) return false;

            bool ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || extra.IndexOf(c) >= 0;
            if (!ok) return false;
        }
        return true;
    }

    // ---------------- Правило 3 ----------------
    private enum CatalogReviewKind { None, WithResult, Empty }

    private static bool ApplyArticleExtraction(TrackedString t, string article)
    {
        string needle = article?.Trim() ?? "";
        if (needle.Length < 2 || t.Length == 0) return false;

        string value = t.Value;
        int search = 0;
        int pos = -1;
        while (search < value.Length)
        {
            int candidate = value.IndexOf(needle, search, StringComparison.OrdinalIgnoreCase);
            if (candidate < 0) break;
            int end = candidate + needle.Length;
            bool leftOk = candidate == 0 || !char.IsLetterOrDigit(value[candidate - 1]);
            bool rightOk = end == value.Length || !char.IsLetterOrDigit(value[end]);
            if (leftOk && rightOk) { pos = candidate; break; }
            search = candidate + 1;
        }
        if (pos < 0) return false;

        if (pos > 0) t.Replace(0, pos, "");
        if (t.Length > needle.Length) t.Replace(needle.Length, t.Length - needle.Length, "");
        return true;
    }

    private static CatalogReviewKind ClassifyCatalogStart(
        string original, string result, ConfigRepository cfg,
        bool startsWithSignificantWord, bool keepSeries)
    {
        // Признак начала значащего слова получен ДО правила 7 (см. Process).
        // Повторно вычислять его по result нельзя: значащие слова уже удалены.
        if (!startsWithSignificantWord) return CatalogReviewKind.None;

        string source = original.Trim();
        string rest = result.Trim();

        // Связь позиции с другим изделием — через предлог («Уплотнения для заслонки»)
        // или через зависимое слово в род. п. («Крышка насоса»): код удаляется.
        // Действует и для кодов из списка серий.
        if (HasPrepositionLink(source) || HasGenitiveLink(source, cfg))
            return CatalogReviewKind.Empty;

        // Код из списка серий («Клапан H74H-16P DN50»): классификатор не применяется.
        if (keepSeries) return CatalogReviewKind.None;

        if (CatalogMarkerRx.IsMatch(source))
            return CatalogReviewKind.WithResult;

        if (string.IsNullOrEmpty(rest)) return CatalogReviewKind.Empty;
        var tokens = TokenSpans(rest).Select(x => rest.Substring(x.start, x.len).Trim(EdgeTrim))
            .Where(x => x.Length > 0).ToList();
        if (tokens.Count == 0) return CatalogReviewKind.Empty;

        // Группа 4: если в остатке есть обозначение стандарта (DIN, ISO, ГОСТ, EN),
        // это техническое обозначение изделия, а не каталожный код — код удаляем.
        if (tokens.Any(t => StandardRx.IsMatch(t))) return CatalogReviewKind.Empty;

        // Fix 3: каталожный номер не засчитываем, если весь остаток — технические токены
        // (резьбы M22X1,5, размеры 88x6x76 ошибочно проходили как каталожный номер)
        bool hasCatalog = CatalogNumberRx.IsMatch(rest) && !tokens.All(IsTechnicalToken);
        bool hasModel = tokens.Any(t => IsIndependentModelToken(t));
        bool hasTech = tokens.Any(IsTechnicalToken);
        bool hasNonTechnicalCode = tokens.Any(t => !IsTechnicalToken(t) && CodeWithTechnicalRx.IsMatch(t));
        // Fix 2: число не считается "кодом рядом с техническим токеном",
        // если оно само является единственным tech-токеном (AISI 420, DIN 439 и т.д.)
        // Fix 5: число сразу после обозначения материала (AISI 316, AISI 420) — это марка
        // стали, а не кодовое обозначение, поэтому standalone-номером оно не считается.
        bool IsMaterialGrade(int i) =>
            i > 0
            && Regex.IsMatch(tokens[i - 1], @"^AISI$", RegexOptions.IgnoreCase)
            && Regex.IsMatch(tokens[i], @"^\d{3}[A-Z]?$", RegexOptions.IgnoreCase);
        bool hasStandaloneNumericCode =
            tokens.Where((t, i) => Regex.IsMatch(t, @"^\d{3,}$") && !IsMaterialGrade(i)).Any()
            && tokens.Any(t => IsTechnicalToken(t) && !Regex.IsMatch(t, @"^\d{3,}$"))
            && tokens.Count > 1;
        bool hasCodeAndTech = hasTech && (hasNonTechnicalCode || hasStandaloneNumericCode) && tokens.Count > 1;

        if (hasCatalog || hasModel || hasCodeAndTech) return CatalogReviewKind.WithResult;
        if (tokens.All(IsTechnicalToken)) return CatalogReviewKind.Empty;
        return CatalogReviewKind.Empty;
    }

    private static bool HasPrepositionLink(string value) =>
        Regex.IsMatch(value, @"(?:^|\s)(?:для|от|for|of|per|к|ко|с|со|на|в|во|из|по|with)\s+[A-ZА-ЯЁ0-9ØΦ]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>
    /// После первого (значащего) слова идёт зависимое существительное в род. п.:
    /// «Крышка насоса», «Ремкомплект распределителя», «Комплект уплотнений клапана».
    /// Прилагательные, отглагольные существительные (-ния/-тия) и слова из файла
    /// «Исключения родительного падежа.txt» пропускаются; просмотр прекращается
    /// на коде, союзе, аббревиатуре или после запятой (перечисление).
    /// Если первое слово — набор («Комплект», «Набор»), следующее слово считается
    /// содержимым набора и пропускается: «Комплект прокладок AB12» не удаляется.
    /// </summary>
    private static bool HasGenitiveLink(string value, ConfigRepository cfg)
    {
        var spans = TokenSpans(value);
        if (spans.Count < 2) return false;

        string head = value.Substring(spans[0].start, spans[0].len).Trim(EdgeTrim);
        bool collective = CollectiveHeads.Contains(head);
        int limit = GenitiveScanLimit + (collective ? 1 : 0);

        for (int k = 1; k < spans.Count && k <= limit; k++)
        {
            string prevRaw = value.Substring(spans[k - 1].start, spans[k - 1].len);
            if (prevRaw.EndsWith(',') || prevRaw.EndsWith(';')) break;   // перечисление

            string w = value.Substring(spans[k].start, spans[k].len).Trim(EdgeTrim);
            if (!CyrWordRx.IsMatch(w)) break;                            // код, число, латиница
            if (OrphanedWords.Contains(w)) break;                        // союз/служебное слово
            if (w.Length <= 4 && w == w.ToUpperInvariant()) break;       // аббревиатура: ПВА, ФУМ

            if (collective && k == 1) continue;                          // содержимое набора

            if (IsGenitiveDependentNoun(w.ToLowerInvariant(), cfg)) return true;
        }
        return false;
    }
        private static bool IsGenitiveDependentNoun(string w, ConfigRepository cfg)
    {
        if (w.Length < 4) return false;
        if (cfg.IsGenitiveException(w)) return false;
        if (AdjSkipEndings.Any(e => w.EndsWith(e, StringComparison.Ordinal))) return false;
        if (w.EndsWith("ния", StringComparison.Ordinal) ||
            w.EndsWith("тия", StringComparison.Ordinal)) return false;  // давления, крепления
        return NounGenitiveEndings.Any(e => w.EndsWith(e, StringComparison.Ordinal));
    }

    /// <summary>
    /// Начинается ли значение со значащего слова (по состоянию ДО правила 7).
    /// </summary>
    /// <summary>Первый токен значения (по состоянию до правила 7).</summary>
    private static string FirstToken(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var first = TokenSpans(value).FirstOrDefault();
        if (first.len == 0) return "";
        return value.Substring(first.start, first.len).Trim(EdgeTrim);
    }

    private static bool StartsWithSignificantWord(string value, ConfigRepository cfg)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var first = TokenSpans(value).FirstOrDefault();
        if (first.len == 0) return false;
        string firstToken = value.Substring(first.start, first.len).Trim(EdgeTrim);
        return firstToken.Length > 0 && cfg.IsSignificantWord(firstToken);
    }

    private static bool IsTechnicalToken(string token)
    {
        string t = token.Trim(EdgeTrim);
        if (t.Length == 0) return true;
        if (IsCyrillicSteelGrade(t, t)) return true;
        return TechnicalTokenRx.IsMatch(t);
    }

    private static bool IsIndependentModelToken(string token)
    {
        string t = token.Trim(EdgeTrim);
        if (t.Length < 2 || IsTechnicalToken(t)) return false;
        if (Regex.IsMatch(t, @"^[A-ZА-ЯЁ]{1,3}\d{1,4}mm$", RegexOptions.IgnoreCase)) return false;
        // Fix 4: обозначение типа соединения (HP-HP, BP-BP, SMB-SMB) — не модельный код.
        // Блокируем только если обе половины одинаковы и длина каждой >= 2 символа.
        if (Regex.IsMatch(t, @"^([A-ZА-ЯЁ]{2,4})(?:-\1)+$", RegexOptions.IgnoreCase)) return false;
        return Regex.IsMatch(t, @"^(?:(?=.*\d)[A-ZА-ЯЁ][A-ZА-ЯЁ0-9]*(?:[-/][A-ZА-ЯЁ0-9]+)*|[A-ZА-ЯЁ]{2,}(?:-[A-ZА-ЯЁ0-9]+)+)$",
            RegexOptions.IgnoreCase);
    }

    private static bool ApplyControlChars(TrackedString t)
    {
        bool changed = false;
        for (int i = t.Length - 1; i >= 0; i--)
        {
            char c = t[i];
            if (Array.IndexOf(ZeroWidth, c) >= 0 || char.IsControl(c) ||
                CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                t.Replace(i, 1, ""); changed = true;
            }
            else if (Array.IndexOf(NbspSources, c) >= 0)
            {
                t.Replace(i, 1, " "); changed = true;   // NBSP -> пробел, иначе склеятся токены
            }
        }
        return changed;
    }

    // ---------------- Правило 6 ----------------
    private static bool ApplyDashes(TrackedString t)
    {
        bool changed = false;
        for (int i = t.Length - 1; i >= 0; i--)
            if (Array.IndexOf(DashSources, t[i]) >= 0) { t.Replace(i, 1, "-"); changed = true; }
        return changed;
    }

    // ---------------- Правило 2 ----------------
    private static bool ApplySpaces(TrackedString t)
    {
        bool changed = false;
        int end = t.Length;
        while (end > 0 && char.IsWhiteSpace(t[end - 1])) end--;
        if (end < t.Length) { t.Replace(end, t.Length - end, ""); changed = true; }

        int start = 0;
        while (start < t.Length && char.IsWhiteSpace(t[start])) start++;
        if (start > 0) { t.Replace(0, start, ""); changed = true; }

        for (int i = t.Length - 1; i >= 0; i--)
            if (char.IsWhiteSpace(t[i]))
            {
                int e = i;
                while (i - 1 >= 0 && char.IsWhiteSpace(t[i - 1])) i--;
                if (e - i + 1 > 1 || t[i] != ' ') { t.Replace(i, e - i + 1, " "); changed = true; }
            }
        return changed;
    }

    // ---------------- Правило 5 ----------------
    private static bool ApplyThread(TrackedString t)
    {
        bool changed = false;
        var matches = ThreadRx.Matches(t.Value).OrderByDescending(m => m.Index).ToList();
        foreach (var m in matches)
        {
            string repl = "M" + m.Groups[2].Value +
                          (m.Groups[3].Success ? "X" + m.Groups[4].Value : "");
            if (t.ReplaceSmart(m.Index, m.Length, repl)) changed = true;
        }
        return changed;
    }

    // ---------------- Правило 4 ----------------
    private static bool ApplyDimensionX(TrackedString t, ProcessOptions opts)
    {
        bool changed = false;
        string v = t.Value;
        foreach (var (start, len) in TokenSpans(v).OrderByDescending(s => s.start))
        {
            string tok = v.Substring(start, len);
            if (ThreadStartRx.IsMatch(tok)) continue;                 // резьба — правило 5
            if (opts.RequireWholeToken && !DimTokenRx.IsMatch(tok)) continue;
            for (int j = tok.Length - 1; j >= 1; j--)
            {
                char c = tok[j];
                if (c != 'х' && c != 'Х' && c != 'X' && c != 'x') continue;
                if (j + 1 >= tok.Length) continue;
                if (!char.IsDigit(tok[j - 1]) || !char.IsDigit(tok[j + 1])) continue;
                if (c == 'x') continue;
                t.Replace(start + j, 1, "x"); changed = true;
            }
        }
        return changed;
    }

    // ---------------- Правило 1 ----------------
    private static bool ApplyDecimal(TrackedString t)
    {
        bool changed = false;
        string v = t.Value;
        var spans = TokenSpans(v);
        for (int k = spans.Count - 1; k >= 0; k--)
        {
            var (start, len) = spans[k];
            string tok = v.Substring(start, len);
            if (DottedNumberRx.IsMatch(tok)) continue;        // 28.58.49 — не трогаем

            var allowed = new List<(int a, int b)>();
            foreach (Match m in ThreadRx.Matches(tok)) allowed.Add((m.Index, m.Index + m.Length));
            if (DimTokenRx.IsMatch(tok)) allowed.Add((0, tok.Length));
            foreach (Match m in NumUnitInTokenRx.Matches(tok)) allowed.Add((m.Index, m.Index + m.Length));

            bool nextIsUnit = k + 1 < spans.Count &&
                              UnitTokenRx.IsMatch(v.Substring(spans[k + 1].start, spans[k + 1].len));
            if (nextIsUnit)
            {
                if (PureDecimalRx.IsMatch(tok)) allowed.Add((0, tok.Length));
                else { var m = TrailDecimalRx.Match(tok); if (m.Success) allowed.Add((m.Index, m.Index + m.Length)); }
            }
            if (allowed.Count == 0) continue;

            // Точки внутри цепочки ***.***.*** не трогаем (AL-302.1302A.01).
            // Исключение — перечисление размеров целиком (66.04x5.33): там две точки,
            // но это десятичные числа, и правило 1 должно сработать.
            var chains = new List<(int a, int b)>();
            if (!DimTokenRx.IsMatch(tok))
                foreach (Match m in DottedChainRx.Matches(tok)) chains.Add((m.Index, m.Index + m.Length));

            for (int j = tok.Length - 2; j >= 1; j--)
            {
                if (tok[j] != '.') continue;
                if (!char.IsDigit(tok[j - 1]) || !char.IsDigit(tok[j + 1])) continue;
                if (chains.Any(sp => j >= sp.a && j < sp.b)) continue;
                if (!allowed.Any(sp => j >= sp.a && j < sp.b)) continue;
                t.Replace(start + j, 1, ","); changed = true;
            }
        }
        return changed;
    }

    // ---------------- Правило 7 ----------------
    private static bool ApplyWords(TrackedString t, ConfigRepository cfg, out bool leadingWordRemoved)
    {
        bool changed = false;
        leadingWordRemoved = false;
        string v = t.Value;
        var spans = TokenSpans(v);

        // Граница уже удалённого «хвоста» в координатах снимка v.
        // Индексы < removedStart в v и в t совпадают, поэтому сверяемся с ней,
        // а не с v.Length (иначе на строках вида «Butterfly valve» пробел
        // захватывался дважды -> выход за границу строки).
        int removedStart = v.Length;

        for (int k = spans.Count - 1; k >= 0; k--)
        {
            var (start, len) = spans[k];
            string tok = v.Substring(start, len);
            string core = tok.Trim(EdgeTrim);
            if (core.Length == 0 || !cfg.IsSignificantWord(core)) continue;

            int s = start, l = len;

            // Забираем один окружающий пробел: сначала слева, иначе справа —
            // но только если он ещё не был удалён вместе с более правым токеном.
            if (s > 0 && char.IsWhiteSpace(v[s - 1])) { s--; l++; }
            else if (s + l < removedStart && char.IsWhiteSpace(v[s + l])) l++;

            if (s + l > removedStart) l = removedStart - s;   // защита от наложения
            if (l <= 0) continue;

            t.Replace(s, l, "");
            removedStart = s;
            changed = true;
            // Первый токен = начало значения (служебные символы и ведущие пробелы
            // к этому моменту уже сняты правилами 3 и 2).
            if (k == 0) leadingWordRemoved = true;
        }
        return changed;
    }

    /// <summary>
    /// Удаляет «осиротевшие» служебные слова — предлоги, союзы, частицы,
    /// которые остались после удаления значащих слов и не несут смысла.
    /// </summary>
    private static void RemoveOrphanedWords(TrackedString t)
    {
        // Повторяем до стабилизации: удаление одного слова может «осиротить» соседнее.
        for (int iter = 0; iter < 5; iter++)
        {
            string v = t.Value;
            var spans = TokenSpans(v);
            if (spans.Count == 0) break;

            bool removed = false;
            // Обход с конца, чтобы индексы не сбивались
            for (int k = spans.Count - 1; k >= 0; k--)
            {
                var (start, len) = spans[k];
                string tok = v.Substring(start, len);
                string core = tok.Trim(EdgeTrim);

                if (core.Length == 0 || !OrphanedWords.Contains(core)) continue;

                // Проверяем, что это действительно «осиротевшее» слово:
                // - либо это единственный оставшийся токен (кроме кодовых)
                // - либо соседние токены — коды (латиница/цифры), а не слова
                bool isOrphaned = IsTokenOrphaned(spans, k, v);
                if (!isOrphaned) continue;

                int s = start, l = len;
                // Захватываем пробел слева или справа
                if (s > 0 && char.IsWhiteSpace(v[s - 1])) { s--; l++; }
                else if (s + l < v.Length && char.IsWhiteSpace(v[s + l])) l++;

                t.Replace(s, l, "");
                removed = true;
                break; // после удаления пересчитываем spans
            }
            if (!removed) break;
        }
    }

    /// <summary>
    /// Проверяет, является ли токен «осиротевшим» — т.е. окружён кодами/служебными словами, а не значащими.
    /// </summary>
    private static bool IsTokenOrphaned(List<(int start, int len)> spans, int idx, string v)
    {
        // Подсчитываем типы токенов
        int codeCount = 0, serviceCount = 0, otherCount = 0;
        foreach (var (st, ln) in spans)
        {
            string tok = v.Substring(st, ln).Trim(EdgeTrim);
            if (tok.Length == 0) continue;
            if (CodeLikeRx.IsMatch(tok) || IsCyrillicSteelGrade(tok, v)) codeCount++;
            else if (OrphanedWords.Contains(tok)) serviceCount++;
            else otherCount++;
        }

        // Если нет «других» слов (только коды и служебные) — все служебные осиротели
        if (otherCount == 0) return true;

        // Проверяем соседей: если оба соседа — коды или служебные или отсутствуют, слово осиротело
        bool leftOk = idx == 0 || IsCodeOrServiceToken(spans[idx - 1], v);
        bool rightOk = idx == spans.Count - 1 || IsCodeOrServiceToken(spans[idx + 1], v);

        return leftOk && rightOk;
    }

    private static bool IsCodeOrServiceToken((int start, int len) span, string v)
    {
        string tok = v.Substring(span.start, span.len).Trim(EdgeTrim);
        if (tok.Length == 0) return true;
        return CodeLikeRx.IsMatch(tok) || IsCyrillicSteelGrade(tok, v) || OrphanedWords.Contains(tok);
    }

    // ---------------- Правило 8 ----------------
    private static bool ApplyEndings(TrackedString t, ConfigRepository cfg,
                                     ProcessOptions opts, string preWords)
    {
        bool changed = false;

        // Опорное значение: состояние до удаления значащих слов.
        // Окончание удаляется только если оно было окончанием и здесь —
        // иначе «ZP 40 Комплект панелей цоколя» потеряло бы значащее «40».
        string anchor = opts.EndingMustBeOriginal ? preWords : "";

        for (int iter = 0; iter < opts.MaxEndingIterations; iter++)
        {
            string v = t.Value;
            if (v.Length == 0) break;

            Match? best = null;
            Match? bestAnchor = null;

            foreach (var rx in cfg.EndingRegexes)
            {
                var m = rx.Match(v);
                if (!m.Success) continue;

                Match? ma = null;
                if (opts.EndingMustBeOriginal)
                {
                    ma = rx.Match(anchor);
                    if (!ma.Success) continue;

                    // Для неявных окончаний (DN**, Ду***) шаблон может совпасть
                    // с разным текстом — требуем именно то же значение.
                    if (!string.Equals(m.Value.Trim(), ma.Value.Trim(),
                                       StringComparison.OrdinalIgnoreCase)) continue;
                }

                if (best == null || m.Length > best.Length) { best = m; bestAnchor = ma; }
            }
            if (best == null) break;

            int s = best.Index, l = best.Length;
            while (s > 0 && char.IsWhiteSpace(v[s - 1])) { s--; l++; }
            if (s == 0 && l >= v.TrimEnd().Length && !opts.AllowEmptyingByEnding) break;

            t.Replace(s, l, "");
            changed = true;

            // Синхронно срезаем то же окончание в опорном значении, чтобы
            // цепочка окончаний («AB 40 60») обрабатывалась до конца.
            if (bestAnchor != null)
            {
                int sa = bestAnchor.Index, la = bestAnchor.Length;
                while (sa > 0 && char.IsWhiteSpace(anchor[sa - 1])) { sa--; la++; }
                anchor = anchor.Remove(sa, la);
            }
        }
        return changed;
    }

    // ---------------- серии без удаления окончаний ----------------
    /// <summary>
    /// Код начинается с серии, для которой окончания не удаляются. Проверяется
    /// значение на момент правила 8 (после правил 3, 6, 2, 5, 4, 1, 7). Правило 10
    /// ещё не применено, поэтому сравнение — без учёта регистра и с приведением
    /// похожих букв по карте замен: серия «H74H» совпадёт и с «Н74Н» на кириллице.
    /// </summary>
    private static bool StartsWithKeepSeries(string value, string? series, CharReplacements map)
    {
        if (string.IsNullOrWhiteSpace(series) || string.IsNullOrEmpty(value)) return false;

        string v = NormalizeForSeries(value.TrimStart(), map);
        foreach (var s in series.Split(new[] { ',', ';', '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string p = NormalizeForSeries(s, map);
            if (p.Length > 0 && v.StartsWith(p, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string NormalizeForSeries(string s, CharReplacements map)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            char u = char.ToUpperInvariant(c);
            if (!map.IsEmpty && map.TryMap(u, out string r)) sb.Append(r);
            else sb.Append(u);
        }
        return sb.ToString().ToUpperInvariant();
    }

    // ---------------- Правило 10 ----------------
    /// <summary>
    /// Замена кириллических букв на совпадающие по написанию латинские.
    /// Работает по значению, УЖЕ обработанному остальными правилами: предохранитель
    /// проверяет остаток кириллицы после удаления значащих слов и окончаний.
    /// </summary>
    private static bool ApplyHomoglyphs(TrackedString t, CharReplacements map, string cyrillicExceptions)
    {
        if (map.IsEmpty) return false;

        string v = t.Value;
        if (v.Length == 0) return false;

        // Есть кириллица вне карты замен — значение не трогаем целиком.
        if (!map.CanApply(v)) return false;

        // Марки стали (08Х18Н10) — обозначение материала, а не латинский код:
        // их символы исключаются из замены.
        var protectedIdx = new HashSet<int>();
        // Токены режутся только по пробелам, поэтому в составных кодах вида
        // F21-50-PN25-10Х17Н13М2Т-TYPE-E марка стали оказывается внутри одного токена
        // и не совпадает с ^...$ целиком. Дополнительно проверяем части, разделённые
        // дефисами и прочими разделителями, и защищаем найденные марки.
        foreach (var (start, len) in TokenSpans(v))
        {
            string token = v.Substring(start, len);
            if (IsCyrillicSteelGrade(token, v))
            {
                for (int k = start; k < start + len; k++) protectedIdx.Add(k);
                continue;
            }

            // Разбор токена на части по разделителям: дефисы, точки, слэши, запятые.
            int partStart = 0;
            for (int p = 0; p <= token.Length; p++)
            {
                bool isDelim = p == token.Length || token[p] == '-' || token[p] == '.'
                    || token[p] == '/' || token[p] == ',' || token[p] == ';';
                if (!isDelim) continue;

                int partLen = p - partStart;
                if (partLen > 0)
                {
                    string part = token.Substring(partStart, partLen);
                    if (IsCyrillicSteelGrade(part, v))
                        for (int k = start + partStart; k < start + partStart + partLen; k++)
                            protectedIdx.Add(k);
                }
                partStart = p + 1;
            }
        }

        // Неизменяемые сочетания кириллицы из настроек: защищаем позиции
        // любого вхождения каждого сочетания в строку (не только целых токенов).
        var cyrExceptions = cyrillicExceptions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Length > 0)
            .ToList();
        if (cyrExceptions.Count > 0)
        {
            foreach (var ex in cyrExceptions)
            {
                int search = 0;
                while (search < v.Length)
                {
                    int pos = v.IndexOf(ex, search, StringComparison.OrdinalIgnoreCase);
                    if (pos < 0) break;
                    for (int k = pos; k < pos + ex.Length; k++) protectedIdx.Add(k);
                    search = pos + 1;
                }
            }
        }
        bool changed = false;

        // Обход с конца: замена 1 символа на N не сдвигает индексы левее текущего.
        // Кириллическая «М» заменяется по общей карте, как остальные буквы:
        // обозначения М004/М0… защищает правило 5 (ThreadRx), здесь отдельной проверки нет.
        for (int i = v.Length - 1; i >= 0; i--)
        {
            if (protectedIdx.Contains(i)) continue;
            if (!map.TryMap(v[i], out string repl)) continue;
            if (repl.Length == 1 && repl[0] == v[i]) continue; // замена на себя же

            t.Replace(i, 1, repl);
            changed = true;
        }
        return changed;
    }
    // ---------------- утилиты ----------------
    public static List<(int start, int len)> TokenSpans(string s)
    {
        var list = new List<(int start, int len)>();
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            int st = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
            list.Add((st, i - st));
        }
        return list;
    }

    /// <summary>Разбиение строки на непрерывные отрезки «подсвечено / не подсвечено».</summary>
    public static IEnumerable<(int start, int len, bool marked)> Segments(string text, HashSet<int> marks)
    {
        int i = 0;
        while (i < text.Length)
        {
            bool m = marks.Contains(i);
            int j = i + 1;
            while (j < text.Length && marks.Contains(j) == m) j++;
            yield return (i, j - i, m);
            i = j;
        }
    }
}