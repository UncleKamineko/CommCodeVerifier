using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommCodeVerifier.Core;

/// <summary>
/// Настройки обработки: состав правил, файлы конфигурации, доп. параметры.
/// Настройки интерфейса (шрифты, окно) хранятся в WPF-проекте.
/// Чтение и запись файла выполняет интерфейс; ошибки (де)сериализации
/// передаются исключением, без вывода сообщений.
/// </summary>
public sealed class ProcessingSettings
{
    public List<RuleId> EnabledRules { get; set; } = RuleCatalog.All.ToList();

    public string? WordsFilePath { get; set; }
    public string? EndingsFilePath { get; set; }
    public string? ExclusionsFilePath { get; set; }
    /// Слова, при которых коммерческий код удаляется безусловно (правило 12).
    public string? ForceDeleteWordsFilePath { get; set; }
    /// Карта замен символов (правило 10).
    public string? CharMapFilePath { get; set; }
    /// Правила проверки комм. кода для ИМ.
    public string? ImRulesFilePath { get; set; }
    /// Правила проверки артикула для ИМ.
    public string? ImArticleRulesFilePath { get; set; }

    /// Версия сохранённых настроек — для подключения новых правил при обновлении.
    public int ConfigVersion { get; set; }

    public MorphologyMode Morphology { get; set; } = MorphologyMode.Paradigms;

    /// Доп. символы, допустимые в «Коды для загрузки в 1С» (по ТЗ — пусто).
    public string Extra1CChars { get; set; } = "";

    /// Сочетания кириллицы, для которых правило 10 не применяется. Разделитель — запятая.
    public string CyrillicExceptions { get; set; } = "";

    /// <summary>
    /// Серии номенклатуры, для которых правило 8 не удаляет окончания,
    /// технические блоки допустимы в форме «* *», классификатор правила 12 не применяется.
    /// Разделитель — запятая, например «H74H,H75H».
    /// </summary>
    public string EndingKeepSeries { get; set; } = "";

    [JsonIgnore]
    public bool UsesCustomConfigFiles =>
        !string.IsNullOrWhiteSpace(WordsFilePath) ||
        !string.IsNullOrWhiteSpace(EndingsFilePath) ||
        !string.IsNullOrWhiteSpace(ExclusionsFilePath) ||
        !string.IsNullOrWhiteSpace(ForceDeleteWordsFilePath) ||
        !string.IsNullOrWhiteSpace(ImRulesFilePath) ||
        !string.IsNullOrWhiteSpace(ImArticleRulesFilePath);

    /// Параметры JSON: отступы, перечисления строками («Paradigms», «DecimalSeparator»).
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Чтение из JSON. Подходит и для settings.json WinForms 0.9.0:
    /// поля совпадают, лишние (шрифты) игнорируются.
    /// </summary>
    /// <exception cref="JsonException">Повреждённый JSON.</exception>
    public static ProcessingSettings FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<ProcessingSettings>(json, JsonOptions) ?? new ProcessingSettings();
        s.Normalize();
        return s;
    }

    public string ToJson()
    {
        Normalize();
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    /// Независимая копия — для отслеживания несохранённых изменений в интерфейсе.
    public ProcessingSettings Clone() => FromJson(ToJson());

    /// Пустой список правил — все правила (как в WinForms); null в строках — пустая строка.
    public void Normalize()
    {
        if (EnabledRules == null || EnabledRules.Count == 0) EnabledRules = RuleCatalog.All.ToList();
        Extra1CChars ??= "";
        CyrillicExceptions ??= "";
        EndingKeepSeries ??= "";
        NormalizeLinkedRules();
    }

    /// Правила 7 и 8 применяются только совместно.
    public void NormalizeLinkedRules()
    {
        foreach (var group in RuleCatalog.LinkedGroups)
        {
            if (group.Any(EnabledRules.Contains))
                foreach (var r in group) if (!EnabledRules.Contains(r)) EnabledRules.Add(r);
        }
        EnabledRules = RuleCatalog.DisplayOrder.Where(EnabledRules.Contains).ToList();
    }

    public ProcessOptions ToProcessOptions() => new()
    {
        Enabled = new HashSet<RuleId>(EnabledRules),
        Extra1CChars = Extra1CChars ?? "",
        CyrillicExceptions = CyrillicExceptions ?? "",
        EndingKeepSeries = EndingKeepSeries ?? ""
    };
}
