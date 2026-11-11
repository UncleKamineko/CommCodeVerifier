using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CommCodeArticle.Core;

/// <summary>
/// Преобразует коммерческий код цилиндра в артикул SRF10
/// по картам ALEN, MID и дополнительного кода.
/// </summary>
public sealed class CylinderArticleGenerator
{
    private static readonly Regex TokenRegex =
        new("[A-Z]+|[0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ExcludedSeries =
        new(StringComparer.OrdinalIgnoreCase) { "19", "QLSM", "QLSN" };

    private static readonly HashSet<string> QLikeSeries =
        new(StringComparer.OrdinalIgnoreCase) { "ST", "MST", "SRR", "SRL" };

    private static readonly string[] KnownSeries =
    {
        "30K", "QCBF", "QCBE", "QCTE", "QCTF", "QDNL", "QDML",
        "QLRM", "QLRN", "QLSM", "QLSN", "QXET", "QPR", "QBM", "QBN",
        "QCB", "QCT", "QDN", "QDM", "QLM", "QLN", "QSM", "QXB", "QXT",
        "QXH", "QN", "QP", "QS", "QR", "MST", "SRR", "SRL", "ST",
        "14", "16", "19", "23", "24", "25", "27", "30", "31", "32",
        "40", "41", "42", "43", "45", "47", "50", "51", "52", "53",
        "54", "60", "61", "62", "63", "67", "68", "69", "70", "90",
        "91", "92", "94", "95", "97", "6"
    };

    private readonly Dictionary<string, string> _mid;
    private readonly Dictionary<string, string> _dop;
    private readonly Dictionary<int, string> _alenLetters;
    private readonly string[] _series;

    public CylinderArticleGenerator(
        string alenMapPath,
        string midMapPath,
        string dopCodeMapPath)
    {
        _alenLetters = LoadAlenMap(alenMapPath);

        _mid = LoadMap(
            midMapPath,
            "Ключ поиска",
            "MID",
            out var mapSeries);

        _dop = LoadMap(
            dopCodeMapPath,
            "Ключ поиска",
            "Доп_код (ожидаемый)",
            out _);

        _series = KnownSeries
            .Concat(mapSeries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => value.Length)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static CylinderArticleGenerator Load() =>
        new(ArticleConfigPaths.AlenMap, ArticleConfigPaths.MidMap, ArticleConfigPaths.DopCodeMap);

    public CylinderArticleResult Generate(
        string? input,
        string? manualAdditionalCode = null)
    {
        try
        {
            return GenerateCore(input, manualAdditionalCode);
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return Result(
                CylinderArticleStatus.ConfigurationError,
                null,
                "Не удалось прочитать карты артикула: " + ex.Message);
        }
    }

    private CylinderArticleResult GenerateCore(
        string? input,
        string? manualAdditionalCode)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Введите коммерческий код.");
        }

        // Раздел 3: уточнения в скобках отбрасываются и дают «**».
        var code = StripParenthetical(Normalize(input), out var hadParenthetical);
        var series = FindSeries(code);

        if (series is null)
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Не удалось определить серию коммерческого кода.");
        }

        if (ExcludedSeries.Contains(series))
        {
            return Result(
                CylinderArticleStatus.ExcludedSeries,
                null,
                "Для данной серии артикул не определён.");
        }

        var working = StripAdministrativeTail(code, series, out var hadAdministrativeTail);

        // П. 9.3: «-EX» с дефисом отбрасывается, «EX» без дефиса — доп. код.
        var hadDashEx = working.EndsWith("-EX", StringComparison.Ordinal);
        if (hadDashEx)
            working = working[..^3];

        // В сериях 30/30K и 69 часть после косой черты участвует в расчёте LEN.
        if (series is not ("30" or "30K" or "69"))
        {
            var slash = working.IndexOf('/');
            if (slash >= 0)
                working = working[..slash];
        }

        var tokens = Tokenize(working);
        if (tokens.Count == 0)
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Не удалось разобрать коммерческий код.");
        }

        var lengthBlock = SelectLengthBlock(working, series, tokens);
        if (string.IsNullOrWhiteSpace(lengthBlock))
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Не удалось определить блок длины.");
        }

        // П. 7.1: две цифры (или две цифры + P) после блока длины —
        // числовой доп. код, назначается вручную.
        var hadNumericAdditionalCode = false;
        var numericTail = Regex.Match(working, @"-\d{2}P?$");
        if (numericTail.Success &&
            series is not ("30" or "30K" or "69" or "43" or "45") &&
            numericTail.Index > working.LastIndexOf(lengthBlock, StringComparison.Ordinal))
        {
            hadNumericAdditionalCode = true;
            working = working[..numericTail.Index];
            tokens = Tokenize(working);
        }

        var alen = EncodeAlen(lengthBlock);
        if (alen is null)
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Не удалось преобразовать блок длины ALEN.");
        }

        var execution = GetExecution(working, series, tokens);
        if (execution is null)
        {
            return Result(
                CylinderArticleStatus.InvalidInput,
                null,
                "Не удалось определить исполнение.");
        }

        var parts = CodeParts.Build(working, series, tokens, lengthBlock);

        var midKey = BuildMidKey(working, series, tokens, parts);
        if (!_mid.TryGetValue(midKey, out var mid))
        {
            return Result(
                CylinderArticleStatus.MidNotFound,
                null,
                "Для данного исполнения не определён блок MID (-xxxx-) артикула, " +
                $"при необходимости обновите файл «MID_карта» (ключ поиска «{midKey}»).");
        }

        var dop = ResolveAdditionalCode(series, parts);

        var hasTail = hadParenthetical || hadAdministrativeTail || hadNumericAdditionalCode;
        var needsManual = dop.Ambiguous || hasTail;
        var manual = string.IsNullOrWhiteSpace(manualAdditionalCode)
            ? null
            : manualAdditionalCode.Trim().ToUpperInvariant();

        // При нескольких блоках доп. код не формируется целиком;
        // при отброшенном хвосте вычисленная часть сохраняется, хвост заменяется «**».
        var dopCode = dop.Ambiguous ? string.Empty : dop.Code;
        if (needsManual)
            dopCode += manual ?? "**";

        var article = $"SRF10-{mid}-{alen}{execution}{dopCode}";
        var tail = article[(article.LastIndexOf('-') + 1)..];
        var tailTooLong = tail.Length > 7;

        var messages = new List<string>();

        if (needsManual && manual is not null)
            messages.Add("Дополнительный код введён вручную и автоматически не проверялся.");
        else if (dop.Ambiguous)
            messages.Add("Спец. исполнение: доп. код автоматически не определяется, " +
                         "введите его вместо **.");
        else if (hasTail)
            messages.Add("Спец. исполнение, введите числовой доп. код вместо **.");
        else
            messages.Add("Артикул сформирован.");

        if (hadDashEx)
            messages.Add("Корректное обозначение взрывозащищённого исполнения — без «-»; суффикс -EX отброшен.");

        if (tailTooLong)
            messages.Add("Последняя часть артикула длиннее 7 символов, установите корректное значение вручную!");

        return Result(
            needsManual
                ? CylinderArticleStatus.ManualAdditionalCode
                : CylinderArticleStatus.Generated,
            article,
            string.Join(" ", messages),
            mid,
            alen,
            execution,
            dopCode,
            tailTooLong);
    }

    private static CylinderArticleResult Result(
        CylinderArticleStatus status,
        string? article,
        string message,
        string? mid = null,
        string? alen = null,
        string? execution = null,
        string? dop = null,
        bool tailTooLong = false)
    {
        return new CylinderArticleResult(
            status,
            article,
            message,
            mid,
            alen,
            execution,
            dop,
            tailTooLong);
    }

    private static List<Match> Tokenize(string code) =>
        TokenRegex.Matches(code).Cast<Match>().ToList();

    private static bool IsQLike(string series) =>
        series.StartsWith('Q') || QLikeSeries.Contains(series);

    private static bool IsDigits(string value) =>
        value.Length > 0 && value.All(char.IsDigit);

    private static bool IsLetters(string value) =>
        value.Length > 0 && value.All(char.IsLetter);

    /// <summary>
    /// Части кода, общие для ключа MID и доп. кода:
    /// блок исполнения, диаметр, буквенный блок за диаметром, опции за длиной.
    /// </summary>
    private sealed class CodeParts
    {
        public Match? Execution { get; private init; }
        public Match? Diameter { get; private init; }
        /// Буквенный блок сразу за диаметром («A», «RL», «F»…).
        public string Option { get; private init; } = "";
        /// Всё после блока длины («N2», «C1V», «EX»…).
        public string Options { get; private init; } = "";
        /// База: начало кода до буквенного блока за исполнением, исполнение = «*» (97M*V).
        public string? Base { get; private init; }

        public static CodeParts Build(
            string code,
            string series,
            IReadOnlyList<Match> tokens,
            string lengthBlock)
        {
            var afterSeries = tokens.Where(t => t.Index >= series.Length).ToList();
            var executionIndex = IsQLike(series) ? 0 : 1;
            var execution = afterSeries.ElementAtOrDefault(executionIndex);

            // П. 4.1: диаметр — первый числовой блок длиной 2–4 после исполнения.
            var diameter = execution is null
                ? null
                : afterSeries
                    .Skip(executionIndex + 1)
                    .FirstOrDefault(t => IsDigits(t.Value) && t.Length is >= 2 and <= 4);

            var option = "";
            if (diameter is not null)
            {
                var next = afterSeries.FirstOrDefault(t => t.Index == diameter.Index + diameter.Length);
                if (next is not null && IsLetters(next.Value))
                    option = next.Value;
            }

            var options = "";
            var lengthIndex = code.LastIndexOf(lengthBlock, StringComparison.Ordinal);
            if (lengthIndex >= 0 &&
                (diameter is null || lengthIndex >= diameter.Index + diameter.Length))
            {
                options = code[(lengthIndex + lengthBlock.Length)..];
            }

            string? baseKey = null;
            var baseToken = afterSeries.ElementAtOrDefault(executionIndex + 1);
            if (execution is not null && baseToken is not null && IsLetters(baseToken.Value))
            {
                baseKey = code[..execution.Index] + "*" +
                          code[(execution.Index + execution.Length)..(baseToken.Index + baseToken.Length)];
            }

            return new CodeParts
            {
                Execution = execution,
                Diameter = diameter,
                Option = option,
                Options = options,
                Base = baseKey
            };
        }
    }

    private static Dictionary<string, string> LoadMap(
        string path,
        string keyHeader,
        string valueHeader,
        out HashSet<string> series)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Не найдена карта артикула.", path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException(
                $"В файле {Path.GetFileName(path)} нет листа.");

        var headerRow = sheet.RowsUsed().FirstOrDefault(row =>
            row.CellsUsed().Any(cell =>
                cell.GetString().Trim().Equals(
                    keyHeader,
                    StringComparison.OrdinalIgnoreCase)));

        if (headerRow is null)
            throw new InvalidDataException(
                $"В файле {Path.GetFileName(path)} не найдена строка заголовков.");

        var headers = headerRow.CellsUsed().ToDictionary(
            cell => cell.GetString().Trim(),
            cell => cell.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);

        if (!headers.TryGetValue(keyHeader, out var keyColumn) ||
            !headers.TryGetValue(valueHeader, out var valueColumn))
        {
            throw new InvalidDataException(
                $"В файле {Path.GetFileName(path)} отсутствует столбец " +
                $"«{keyHeader}» или «{valueHeader}».");
        }

        series = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (headers.TryGetValue("Серия", out var seriesColumn))
        {
            foreach (var row in sheet.RowsUsed()
                         .Where(row => row.RowNumber() > headerRow.RowNumber()))
            {
                var seriesValue = row.Cell(seriesColumn)
                    .GetFormattedString()
                    .Trim();

                if (seriesValue.Length > 0)
                    series.Add(seriesValue);
            }
        }

        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in sheet.RowsUsed()
                     .Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            var key = row.Cell(keyColumn).GetFormattedString().Trim();
            var value = row.Cell(valueColumn).GetFormattedString().Trim();

            if (key.Length == 0 || value.Length == 0)
                continue;

            if (result.TryGetValue(key, out var previous) &&
                !string.Equals(previous, value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Ключ «{key}» в файле {Path.GetFileName(path)} " +
                    "имеет неоднозначные значения.");
            }

            result[key] = value;
        }

        return result;
    }

    private static Dictionary<int, string> LoadAlenMap(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Не найдена карта ALEN.", path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("В карте ALEN нет листа.");

        var header = sheet.RowsUsed()
            .SelectMany(row => row.CellsUsed())
            .FirstOrDefault(cell =>
                cell.GetString().Trim() == "Блок LEN (сотни)")
            ?? throw new InvalidDataException(
                "В карте ALEN не найден заголовок «Блок LEN (сотни)».");

        var letterHeader = sheet.Row(header.Address.RowNumber)
            .CellsUsed()
            .FirstOrDefault(cell =>
                cell.GetString().Trim() == "Код в артикуле")
            ?? throw new InvalidDataException(
                "В карте ALEN не найден столбец «Код в артикуле».");

        var result = new Dictionary<int, string>();

        foreach (var row in sheet.RowsUsed()
                     .Where(row => row.RowNumber() > header.Address.RowNumber))
        {
            var blockText = row.Cell(header.Address.ColumnNumber)
                .GetFormattedString()
                .Trim();

            if (!int.TryParse(
                    blockText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var hundreds))
            {
                continue;
            }

            var code = row.Cell(letterHeader.Address.ColumnNumber)
                .GetFormattedString()
                .Trim();

            if (code.Length > 0)
                result[hundreds] = code;
        }

        if (result.Count == 0)
            throw new InvalidDataException(
                "В карте ALEN отсутствуют буквенные коды сотен.");

        return result;
    }

    private string? FindSeries(string code)
    {
        foreach (var candidate in _series)
        {
            if (!code.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                continue;

            if (candidate.Length == code.Length)
                return candidate;

            var next = code[candidate.Length];

            if (candidate.All(char.IsDigit))
            {
                if (candidate == "30" && next == 'K')
                    return "30K";

                // Цифровая серия: за ней буква или дефис, иначе это другая серия (40 ≠ 4…).
                if (char.IsLetter(next) || next == '-')
                    return candidate;

                continue;
            }

            // Буквенная серия: за ней не буква, иначе это более длинная серия (QCB ≠ QCBF).
            if (!char.IsLetter(next))
                return candidate;
        }

        return null;
    }

    private static string Normalize(string input)
    {
        var replacements = new Dictionary<char, char>
        {
            ['А'] = 'A',
            ['В'] = 'B',
            ['Е'] = 'E',
            ['К'] = 'K',
            ['М'] = 'M',
            ['Н'] = 'H',
            ['О'] = 'O',
            ['Р'] = 'P',
            ['С'] = 'C',
            ['Т'] = 'T',
            ['У'] = 'Y',
            ['Х'] = 'X',
            ['а'] = 'A',
            ['в'] = 'B',
            ['е'] = 'E',
            ['к'] = 'K',
            ['м'] = 'M',
            ['н'] = 'H',
            ['о'] = 'O',
            ['р'] = 'P',
            ['с'] = 'C',
            ['т'] = 'T',
            ['у'] = 'Y',
            ['х'] = 'X'
        };

        var builder = new StringBuilder();

        foreach (var source in input.Trim())
        {
            if (char.IsWhiteSpace(source))
                continue;

            var upper = char.ToUpperInvariant(source);

            if (replacements.TryGetValue(source, out var latin) ||
                replacements.TryGetValue(upper, out latin))
            {
                builder.Append(latin);
            }
            else
            {
                builder.Append(upper);
            }
        }

        return builder.ToString();
    }

    private static string StripParenthetical(string code, out bool stripped)
    {
        // Уточнения в скобках вырезаются, текст после скобок сохраняется:
        // 67MP2C032A0010(2)GFR -> 67MP2C032A0010GFR.
        var result = Regex.Replace(code, @"\([^()]*\)", "");

        var open = result.IndexOf('(');
        if (open >= 0)
            result = result[..open];

        stripped = result.Length != code.Length;
        return result;
    }

    private static string StripAdministrativeTail(string code, string series, out bool stripped)
    {
        // Для 30/30K инструкция сохраняет часть кода после дефиса -3.
        var pattern = series is "30" or "30K"
            ? @"-(RF\d{2}|RC\d{2}|RU\d{2}|ESC|IL|KZ\d{2})[A-Z0-9]*$"
            // -EX обрабатывается отдельно: дефисное EX отбрасывается,
            // а EX без дефиса остаётся доп. кодом.
            : @"-(RF\d{2}|RF|RC\d{2}|RC|RU\d{2}|ESC|IL|KZ\d{2}|PTG|PTR|REG[A-Z0-9]*)[A-Z0-9]*$";

        var result = Regex.Replace(code, pattern, "", RegexOptions.IgnoreCase);
        stripped = result.Length != code.Length;
        return result;
    }

    private static string? SelectLengthBlock(
        string code,
        string series,
        IReadOnlyList<Match> tokens)
    {
        if (series == "QR")
            return "000";

        if (series is "30" or "30K" or "69")
        {
            var slash = code.IndexOf('/');
            if (slash >= 0)
            {
                var match = Regex.Match(code[(slash + 1)..], @"^(\d{2,5})");
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }

        var special40 = Regex.Match(
            code,
            @"^40[A-Z]\dG-[^-]+-(\d+)");

        if (special40.Success)
            return special40.Groups[1].Value;

        var numericBlocks = tokens
            .Where(token =>
                token.Index >= series.Length &&
                IsDigits(token.Value))
            .ToList();

        if (numericBlocks.Count == 0)
            return null;

        // П. 5.1: для серии с цифры — 4-й числовой блок с учётом самой серии,
        // для серии с буквы — 3-й числовой. В обоих случаях это 3-й числовой
        // блок после серии; если блоков меньше — последний.
        return numericBlocks.Count >= 3
            ? numericBlocks[2].Value
            : numericBlocks[^1].Value;
    }

    private string? EncodeAlen(string value)
    {
        if (!value.All(char.IsDigit))
            return null;

        if (value.Length <= 3)
            return value.PadLeft(3, '0');

        if (value.Length != 4)
            return value;

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var millimetres))
        {
            return null;
        }

        if (millimetres < 1000)
            return millimetres.ToString("000", CultureInfo.InvariantCulture);

        return _alenLetters.TryGetValue(millimetres / 100, out var letters)
            ? letters + (millimetres % 100).ToString("00", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? GetExecution(
        string code,
        string series,
        IReadOnlyList<Match> tokens)
    {
        if (series is "43" or "45" or "MST")
            return "0";

        if (series is "30" or "30K")
            return code.EndsWith("-3", StringComparison.Ordinal) ? "3" : "2";

        if (series == "69")
        {
            if (Regex.IsMatch(code, @"-(F|FW)$"))
                return "0";

            if (Regex.IsMatch(code, @"-(M|MW)$"))
                return "2";
        }

        if (series is "QCT" or "QCTE" or "QCTF" or "QCB" or "QCBE" or
            "QN" or "QS" or "QXB" or "QXT")
        {
            return "3";
        }

        var afterSeries = tokens
            .Where(token => token.Index >= series.Length)
            .ToList();

        // П. 6.1: QR — исполнение по Б3 (QR | 07 | A).
        if (series == "QR")
        {
            var block = afterSeries.ElementAtOrDefault(1)?.Value;
            return block is { Length: 1 } ? block : null;
        }

        // П. 6.9: ST — последняя цифра Б2 (ST312 -> 2).
        if (series == "ST")
        {
            var block = afterSeries.ElementAtOrDefault(0)?.Value;
            return block is not null && IsDigits(block) ? block[^1..] : null;
        }

        var executionIndex = IsQLike(series) ? 0 : 1;
        var execution = afterSeries.ElementAtOrDefault(executionIndex)?.Value;

        if (execution is null)
            return null;

        // П. 6.4: QLRM, QLRN по Б2.
        if (series is "QLRM" or "QLRN")
        {
            return execution switch
            {
                "1" => "1",
                "2" => "3",
                "3" => "3",
                "4" => "4",
                _ => null
            };
        }

        if (series is "16" or "24" or "31" or "32" or "94" ||
            series.StartsWith("QP", StringComparison.Ordinal) ||
            series is "QLM" or "QLN")
        {
            return execution switch
            {
                "1" => "1",
                "2" => "3",
                "3" => "9",
                "4" => "A",
                _ => null
            };
        }

        // Краткая справка: 67 — Б3, но при Б4 = N исполнение 3.
        if (series == "67" &&
            afterSeries.ElementAtOrDefault(executionIndex + 1)?.Value == "N")
        {
            return "3";
        }

        if (series == "25")
        {
            return execution switch
            {
                "1" => "1",
                "2" => "2",
                "3" => "6",
                _ => null
            };
        }

        if (series == "27")
        {
            return execution switch
            {
                "1" => "1",
                "2" => "3",
                "4" => "4",
                "6" => "9",
                _ => null
            };
        }

        if (series == "52")
        {
            return execution switch
            {
                "2" => "2",
                "8" => "B",
                _ => null
            };
        }

        if (series == "95")
        {
            return execution switch
            {
                "2" => "2",
                "3" => "6",
                _ => null
            };
        }

        if (series == "63")
        {
            return execution switch
            {
                "1" => "1",
                "2" => "2",
                "6" => "6",
                "7" => "7",
                "9" => "A",
                _ => null
            };
        }

        return execution.Length == 1 &&
               char.IsLetterOrDigit(execution[0])
            ? execution
            : null;
    }

    private string BuildMidKey(
        string code,
        string series,
        IReadOnlyList<Match> tokens,
        CodeParts parts)
    {
        if (series is "30" or "30K")
        {
            var match = Regex.Match(code, @"^30K?-(\d{3})");
            return match.Success ? "30-" + match.Groups[1].Value : code;
        }

        if (series == "69")
        {
            var match = Regex.Match(code, @"^69-(\d{3})");
            return match.Success ? "69-" + match.Groups[1].Value : code;
        }

        if (series == "QR")
        {
            var match = Regex.Match(code, @"^QR\d{2}");
            return match.Success ? match.Value : code;
        }

        // П. 4.4: ключ MST — серия + диаметр (MST6); MST1 — это уже MID.
        if (series == "MST")
        {
            var match = Regex.Match(code, @"^MST\d+");
            return match.Success ? match.Value : code;
        }

        // П. 4.4: 43, 45 — серия + Б2 + Б3 (43N-40-40 -> 43N40).
        if (series is "43" or "45")
        {
            var key = string.Concat(tokens.Take(2).Select(t => t.Value));
            var third = tokens.ElementAtOrDefault(2);
            if (third is not null && IsDigits(third.Value))
                key += third.Value;
            return key;
        }

        string template;

        if (series == "ST")
        {
            // П. 4.4: Б2 без последней цифры + «*» + блоки до диаметра (ST32*A020).
            var afterSeries = tokens.Where(t => t.Index >= series.Length).ToList();
            var block = afterSeries.ElementAtOrDefault(0);
            if (block is null || !IsDigits(block.Value) || block.Length < 2 || parts.Diameter is null)
                return code;

            var blockEnd = block.Index + block.Length;
            template = "ST" + block.Value[..^1] + "*" +
                       code[blockEnd..(parts.Diameter.Index + parts.Diameter.Length)];
        }
        else
        {
            if (parts.Execution is null || parts.Diameter is null)
                return code;

            var end = parts.Diameter.Index + parts.Diameter.Length;
            template =
                code[..parts.Execution.Index] +
                "*" +
                code[(parts.Execution.Index + parts.Execution.Length)..end];
        }

        var variantPrefix = template + "#";
        var variants = _mid.Keys
            .Where(key => key.StartsWith(variantPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => key[variantPrefix.Length..])
            .Where(value => value.Length > 0)
            .ToList();

        if (variants.Count == 0)
            return template;

        // Особые серии: MID различают опции за длиной.
        if (series is "63" or "31" or "32" or "67")
        {
            var options = parts.Options;

            // П. 7.8: GFR — спец. исполнение, на MID не влияет.
            if (series == "67" && options.StartsWith("GFR", StringComparison.Ordinal))
                return template;

            // П. 7.7: в серии 63 G приоритетнее C и составных опций (C2G, WG -> G).
            if (series == "63" && options.Contains('G') &&
                variants.Contains("G", StringComparer.OrdinalIgnoreCase))
            {
                return variantPrefix + "G";
            }

            var variant = variants
                .OrderByDescending(value => value.Length)
                .FirstOrDefault(value => options.StartsWith(value, StringComparison.OrdinalIgnoreCase));

            return variant is null ? template : variantPrefix + variant;
        }

        // Дискриминирующие серии: MID различает буква сразу за диаметром.
        var letter = variants.FirstOrDefault(value =>
            value.Equals(parts.Option, StringComparison.OrdinalIgnoreCase));

        return letter is null ? template : variantPrefix + letter;
    }

    /// <summary>
    /// Доп. код по карте. Ключи карты:
    /// «ПРЕФИКС#ОПЦИЯ|ОКОНЧАНИЕ» — сочетание опции за диаметром и окончания (A — обычная опция);
    /// «ПРЕФИКС#ОПЦИЯ#» — опция за диаметром; «БАЗА#» — по началу кода.
    /// ПРЕФИКС — серия или база (97M*V). Если доп. код дают несколько
    /// независимых блоков, изделие специальное: Ambiguous = true (п. 7.6).
    /// </summary>
    private (string Code, bool Ambiguous) ResolveAdditionalCode(
        string series,
        CodeParts parts)
    {
        // П. 7.4: доп. код SRL/SRR привязан к серии.
        if (series == "SRL")
            return ("L0", false);

        if (series == "SRR")
            return ("R0", false);

        // П. 7.8: GFR в серии 67 — доп. код назначается вручную.
        if (series == "67" && parts.Options.Contains("GFR", StringComparison.Ordinal))
            return (string.Empty, true);

        var prefixes = parts.Base is null
            ? new[] { series }
            : new[] { parts.Base, series };

        var option = parts.Option.Length > 0 ? parts.Option : "A";

        // Окончание — самое длинное из окончаний карты, которым заканчиваются опции за длиной.
        var ending = parts.Options.Length == 0
            ? null
            : _dop.Keys
                .SelectMany(key => prefixes
                    .Where(prefix => key.StartsWith(prefix + "#", StringComparison.OrdinalIgnoreCase))
                    .Select(prefix => key[(prefix.Length + 1)..]))
                .Select(rest => rest.IndexOf('|') is var bar and >= 0 ? rest[(bar + 1)..] : null)
                .Where(value => !string.IsNullOrEmpty(value) &&
                                parts.Options.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(value => value!.Length)
                .FirstOrDefault();

        // Сочетание опции и окончания задано в карте явно (QCT#D|EX, 40#A|V).
        if (ending is not null)
        {
            foreach (var prefix in prefixes)
            {
                if (_dop.TryGetValue($"{prefix}#{option}|{ending}", out var combined))
                    return (combined, false);
            }
        }

        var found = new List<string>();

        var optionValue = prefixes
            .Select(prefix => _dop.TryGetValue($"{prefix}#{option}#", out var value) ? value : null)
            .FirstOrDefault(value => value is not null);
        if (optionValue is not null)
            found.Add(optionValue);

        if (ending is not null)
        {
            var endingValue = prefixes
                .Select(prefix => _dop.TryGetValue($"{prefix}#A|{ending}", out var value) ? value : null)
                .FirstOrDefault(value => value is not null);
            if (endingValue is not null)
                found.Add(endingValue);
        }

        if (parts.Base is not null && _dop.TryGetValue(parts.Base + "#", out var baseValue))
            found.Add(baseValue);

        return found.Count switch
        {
            0 => (string.Empty, false),
            1 => (found[0], false),
            _ => (string.Empty, true)
        };
    }
}
