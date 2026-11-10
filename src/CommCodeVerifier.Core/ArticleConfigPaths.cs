namespace CommCodeVerifier.Core;

public static class ArticleConfigPaths
{
    public const string FolderNameGeneral = "Файлы конфигурации артикул";
    public const string FolderNameCyl = "Цилиндры";
    public static string Folder =>
        Path.Combine(AppPaths.ConfigFolder, FolderNameGeneral, FolderNameCyl);

    public static string AlenMap =>
        Path.Combine(Folder, "ALEN_карта.xlsx");

    public static string MidMap =>
        Path.Combine(Folder, "MID_карта.xlsx");

    public static string DopCodeMap =>
        Path.Combine(Folder, "Доп_код_карта.xlsx");

    public static string Instruction =>
        Path.Combine(Folder, "ПРЕОБРАЗОВАНИЕ_КОДА_В_АРТИКУЛ.txt");

    public static string ResolveAlenMap(ProcessingSettings settings) =>
        Resolve(settings.ArticleAlenMapFilePath, AlenMap);

    public static string ResolveMidMap(ProcessingSettings settings) =>
        Resolve(settings.ArticleMidMapFilePath, MidMap);

    public static string ResolveDopCodeMap(ProcessingSettings settings) =>
        Resolve(settings.ArticleDopCodeMapFilePath, DopCodeMap);

    private static string Resolve(string? customPath, string defaultPath) =>
        !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath)
            ? customPath
            : defaultPath;
}
