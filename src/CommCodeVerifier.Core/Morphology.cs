using System.Text.RegularExpressions;

namespace CommCodeVerifier.Core;

/// <summary>Генерация словоформ для списка значащих слов (ответ на вопрос 1 ТЗ).</summary>
public static class Morphology
{
    private static readonly HashSet<string> FunctionWords = new(StringComparer.OrdinalIgnoreCase)
    { "для","без","из","на","по","при","и","или","the","and","or","for","with","of","in","on","by","to","a","an" };

    private static readonly Dictionary<string, string[]> EnIrregular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["series"] = new[] { "series" },
        ["knife"] = new[] { "knives" },
        ["body"] = new[] { "bodies" },
        ["bushing"] = new[] { "bushings" },
        ["box"] = new[] { "boxes" },
    };

    // --- таблицы окончаний ---
    private static readonly string[] AdjHard = { "ый", "ого", "ому", "ым", "ом", "ая", "ой", "ую", "ою", "ое", "ые", "ых", "ыми" };
    private static readonly string[] AdjSoft = { "ий", "его", "ему", "им", "ем", "яя", "ей", "юю", "ее", "ие", "их", "ими" };
    private static readonly string[] NounFemA = { "а", "ы", "е", "у", "ой", "ою", "и", "ам", "ами", "ах", "" };
    private static readonly string[] NounFemYa = { "я", "и", "е", "ю", "ей", "ею", "ям", "ями", "ях", "й" };
    private static readonly string[] NounMascHard = { "", "а", "у", "ом", "е", "ы", "ов", "ам", "ами", "ах" };
    private static readonly string[] NounMascSoft = { "ь", "я", "ю", "ем", "е", "и", "ей", "ям", "ями", "ях" };
    private static readonly string[] NounNeutO = { "о", "а", "у", "ом", "е", "ам", "ами", "ах", "" };
    private static readonly string[] NounNeutIe = { "е", "я", "ю", "ем", "и", "й", "ям", "ями", "ях" };
    private static readonly string[] NounFemSoft = { "ь", "и", "ью", "ей", "ям", "ями", "ях" };
    private static readonly string[] Universal =
    {
        "","а","я","у","ю","ы","и","е","ой","ей","ою","ею","ом","ем","ам","ям","ах","ях","ами","ями",
        "ов","ев","ий","ый","ая","яя","ое","ее","ые","ие","ых","их","ым","им","ыми","ими","ую","юю",
        "ого","его","ому","ему","ь","й"
    };

    private const string Vowels = "аеёиоуыэюя";

    private static readonly Regex RussianWordRx = new(@"^[а-яё\-]+$", RegexOptions.Compiled);
    private static readonly Regex EnglishWordRx = new(@"^[a-z\-]+$", RegexOptions.Compiled);

    public static IEnumerable<string> Expand(string word, MorphologyMode mode)
    {
        string w = word.Trim().ToLowerInvariant();
        var result = new HashSet<string>(StringComparer.Ordinal) { w };
        if (mode == MorphologyMode.Off || w.Length < 3 || FunctionWords.Contains(w)) return result;

        if (RussianWordRx.IsMatch(w)) AddRussian(w, mode, result);
        else if (EnglishWordRx.IsMatch(w)) AddEnglish(w, result);

        // Формы короче 3 символов слишком опасны: могут совпасть с фрагментом кода.
        result.RemoveWhere(f => f.Length < 3);
        result.Add(w);
        return result;
    }

    // ---------------- русский ----------------
    private static void AddRussian(string w, MorphologyMode mode, HashSet<string> acc)
    {
        if (mode == MorphologyMode.Aggressive)
        {
            foreach (var stem in CandidateStems(w))
                foreach (var e in Universal) acc.Add(stem + e);
            return;
        }

        string end2 = w.Length >= 4 ? w[^2..] : "";
        string stem2 = w.Length >= 4 ? w[..^2] : "";
        string stem1 = w[..^1];

        // --- прилагательные и существительные на -ие/-ее (диспетчер по двум последним буквам) ---
        switch (end2)
        {
            case "ый":
            case "ой":
            case "ые":
                Emit(stem2, AdjHard, acc);
                Emit(stem2, AdjSoft, acc);
                return;

            case "ий":
            case "ие":
                // Неоднозначность: «средние» — прилагательное, «уплотнение» — существительное.
                // Генерируем обе парадигмы: избыточные формы безвредны, недостающие — нет.
                Emit(stem2, AdjSoft, acc);
                Emit(stem2, AdjHard, acc);
                Emit(stem1, NounNeutIe, acc);   // основа именно stem1: уплотнени + я/й/ем/и
                return;

            case "ая":
                Emit(stem2, AdjHard, acc);
                return;

            case "яя":
                Emit(stem2, AdjSoft, acc);
                return;

            case "ое":
                Emit(stem2, AdjHard, acc);
                return;

            case "ее":
                Emit(stem2, AdjSoft, acc);
                Emit(stem1, NounNeutIe, acc);
                return;
        }

        // --- существительные по последней букве ---
        switch (w[^1])
        {
            case 'а':
                EmitFem(stem1, acc);
                Emit(stem1, NounNeutO, acc);
                Emit(stem1, NounMascHard, acc);
                break;

            case 'я':
                Emit(stem1, NounFemYa, acc);
                Emit(stem1, NounNeutIe, acc);
                break;

            case 'о':
                Emit(stem1, NounNeutO, acc);
                AddFleeting(stem1, acc);        // кольцо -> колец
                break;

            case 'е':
                Emit(stem1, NounNeutIe, acc);
                break;

            case 'ь':
                Emit(stem1, NounFemSoft, acc);
                Emit(stem1, NounMascSoft, acc);
                break;

            case 'й':
                Emit(stem1, NounMascSoft, acc);
                break;

            case 'и':
            case 'ы':                           // словарь содержит форму мн. ч.: «краны», «фильтры»
                EmitFem(stem1, acc);
                Emit(stem1, NounMascHard, acc);
                Emit(stem1, NounNeutO, acc);
                break;

            default:                            // согласная: затвор, винт, фильтр
                Emit(w, NounMascHard, acc);
                if (w.EndsWith("ок") || w.EndsWith("ек") || w.EndsWith("ец"))
                    Emit(w[..^2] + w[^1], NounMascHard, acc);   // замок -> замка
                break;
        }
    }

    private static IEnumerable<string> CandidateStems(string w)
    {
        yield return w;
        if (w.Length > 3) yield return w[..^1];
        if (w.Length > 4) yield return w[..^2];
    }

    private static void EmitFem(string stem, HashSet<string> acc)
    {
        Emit(stem, NounFemA, acc);
        AddFleeting(stem, acc);
    }

    /// <summary>
    /// Беглая гласная в родительном падеже мн. ч.:
    /// заслонк -> заслонок, крышк -> крышек, гайк -> гаек, кольц -> колец.
    /// </summary>
    private static void AddFleeting(string stem, HashSet<string> acc)
    {
        if (stem.Length < 3) return;

        char last = stem[^1];
        char prev = stem[^2];
        if (Vowels.IndexOf(prev) >= 0) return;                       // нет стыка согласных
        if ("кгхцчжшщкнрлмвбпдтсз".IndexOf(last) < 0) return;

        if (prev == 'й')
        {
            acc.Add(stem[..^2] + "е" + last);                        // гайк -> гаек
            return;
        }

        char ins = "ьчщжш".IndexOf(prev) >= 0 ? 'е' : 'о';
        acc.Add(stem[..^1] + ins + last);
        acc.Add(stem[..^1] + 'е' + last);                            // подстраховка для обоих вариантов
    }

    private static void Emit(string stem, string[] endings, HashSet<string> acc)
    {
        if (stem.Length < 2) return;
        foreach (var e in endings) acc.Add(stem + e);
    }

    // ---------------- английский ----------------
    private static void AddEnglish(string w, HashSet<string> acc)
    {
        if (EnIrregular.TryGetValue(w, out var forms))
        {
            foreach (var f in forms) acc.Add(f);
            return;
        }

        // словарное слово во мн. ч. -> ед. ч.
        if (w.EndsWith("ies") && w.Length > 4) acc.Add(w[..^3] + "y");
        else if (w.EndsWith("ves") && w.Length > 4) { acc.Add(w[..^3] + "f"); acc.Add(w[..^3] + "fe"); }
        else if (w.EndsWith("es") && w.Length > 3) { acc.Add(w[..^2]); acc.Add(w[..^1]); }
        else if (w.EndsWith("s") && w.Length > 3 && !w.EndsWith("ss")) acc.Add(w[..^1]);

        // словарное слово в ед. ч. -> мн. ч.
        if (w.EndsWith("s") || w.EndsWith("x") || w.EndsWith("z") ||
            w.EndsWith("ch") || w.EndsWith("sh")) acc.Add(w + "es");
        else if (w.EndsWith("y") && w.Length > 2 && "aeiou".IndexOf(w[^2]) < 0) acc.Add(w[..^1] + "ies");
        else if (w.EndsWith("fe")) { acc.Add(w[..^2] + "ves"); acc.Add(w + "s"); }
        else if (w.EndsWith("f")) { acc.Add(w[..^1] + "ves"); acc.Add(w + "s"); }
        else acc.Add(w + "s");
    }
}