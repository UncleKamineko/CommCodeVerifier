using System.Text;

namespace CommCodeVerifier.Core;

public static class MissingImCodeParser
{
    public static MissingImCodeStructure Parse(
        string article,
        string commercialCode)
    {
        article ??= "";
        commercialCode ??= "";

        var fragments = new List<MissingImFragment>();
        var separators = new List<string>();

        int position = 0;
        int fragmentPosition = 0;

        while (position < commercialCode.Length)
        {
            char current = commercialCode[position];

            // В данном случае мы обрабатываем буквы, цифры, разделители и другие символы.
            // Сюда возможно потребуется добавить дополнительные условия для обработки других типов символов,
            // например, если нужно обрабатывать пробелы, специальные символы или букво-цифровые последовательности.
            // Предварительно добавлять через ParseMixedAlphaNumericFragment(...) и ParseFractionFragment(...)

            // Если текущий символ является буквой, мы собираем последовательность букв и добавляем её как фрагмент.
            if (char.IsLetter(current))
            {
                int start = position;

                while (position < commercialCode.Length &&
                       char.IsLetter(commercialCode[position]))
                {
                    position++;
                }

                AddFragment(
                    fragments,
                    fragmentPosition++,
                    commercialCode[start..position],
                    MissingImFragmentType.Letters);

                continue;
            }
            // Если текущий символ является цифрой, мы собираем последовательность цифр и добавляем её как фрагмент.
            if (char.IsDigit(current))
            {
                int start = position;

                while (position < commercialCode.Length &&
                       char.IsDigit(commercialCode[position]))
                {
                    position++;
                }

                AddFragment(
                    fragments,
                    fragmentPosition++,
                    commercialCode[start..position],
                    MissingImFragmentType.Digits);

                continue;
            }

            // Если текущий символ является разделителем, мы собираем последовательность разделителей и добавляем её как фрагмент.
            if (IsSeparator(current))
            {
                int start = position;

                while (position < commercialCode.Length &&
                       IsSeparator(commercialCode[position]))
                {
                    position++;
                }

                string separator =
                    commercialCode[start..position];

                separators.Add(separator);

                AddFragment(
                    fragments,
                    fragmentPosition++,
                    separator,
                    MissingImFragmentType.Separator);

                continue;
            }

            int otherStart = position;

            while (position < commercialCode.Length &&
                   !char.IsLetterOrDigit(commercialCode[position]) &&
                   !IsSeparator(commercialCode[position]))
            {
                position++;
            }

            AddFragment(
                fragments,
                fragmentPosition++,
                commercialCode[otherStart..position],
                MissingImFragmentType.Other);
        }

        return new MissingImCodeStructure
        {
            Article = article,
            CommercialCode = commercialCode,
            FirstRequiredFragment = MissingImFirstFragment.Extract(commercialCode),
            Fragments = fragments,
            Separators = separators
        };
    }

    private static void AddFragment(
        ICollection<MissingImFragment> fragments,
        int position,
        string text,
        MissingImFragmentType type)
    {
        if (text.Length == 0)
            return;

        fragments.Add(
            new MissingImFragment(
                position,
                text,
                type,
                text.Length));
    }

    private static bool IsSeparator(char value) =>
        value is '-' or '/' or '.' or ',' or '(' or ')';
}
