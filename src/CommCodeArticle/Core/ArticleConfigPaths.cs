using System.IO;
namespace CommCodeArticle.Core;

/// <summary>
/// Пути файлов конфигурации артикула. Структура папок совпадает с CommCodeVerifier:
/// «Файлы конфигурации\Файлы конфигурации артикул\Цилиндры» рядом с exe.
/// </summary>
public static class ArticleConfigPaths
{
    public static string BaseFolder => AppContext.BaseDirectory;

    public static string Folder =>
        Path.Combine(BaseFolder, "Файлы конфигурации", "Файлы конфигурации артикул", "Цилиндры");

    public static string AlenMap => Path.Combine(Folder, "ALEN_карта.xlsx");

    public static string MidMap => Path.Combine(Folder, "MID_карта.xlsx");

    public static string DopCodeMap => Path.Combine(Folder, "Доп_код_карта.xlsx");

    public static string Instruction => Path.Combine(Folder, "ПРЕОБРАЗОВАНИЕ_КОДА_В_АРТИКУЛ.txt");
}
