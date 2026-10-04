using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;

namespace CommCodeVerifier.Wpf.Views;

/// <summary>
/// Окно справки: Assets\Help.rtf. Немодальное, одно (см. WindowService).
/// Esc — закрыть, Ctrl + колесо — масштаб.
/// </summary>
public partial class HelpWindow : Window
{
    public const string HelpFileName = "Help.rtf";

    /// Штатное расположение файла справки.
    public static string HelpFolder => Path.Combine(AppPaths.BaseFolder, "Assets");
    public static string HelpFilePath => Path.Combine(HelpFolder, HelpFileName);

    // Адреса в обычном тексте — как DetectUrls в WinForms.
    private static readonly Regex UrlRx = new(@"(?:https?://|www\.)[^\s<>""«»]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;

    public HelpWindow(IShellService shell, IDialogService dialogs)
    {
        InitializeComponent();
        _shell = shell;
        _dialogs = dialogs;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Viewer.PreviewMouseWheel += OnViewerWheel;
        Viewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));

        LoadContent();
    }

    // ================= загрузка =================
    private void LoadContent()
    {
        string? path = ResolveHelpFile();
        if (path == null)
        {
            ShowPlaceholder();
            return;
        }

        try
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(12),
                FontFamily = new FontFamily("Segoe UI")
            };
            // FileShare.ReadWrite — файл можно держать открытым в редакторе.
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                new TextRange(doc.ContentStart, doc.ContentEnd).Load(fs, DataFormats.Rtf);

            LinkifyUrls(doc);
            Viewer.Document = doc;
            SetStatus("Файл справки: " + path, error: false);
        }
        catch (Exception ex)
        {
            // Повреждённый или не-RTF файл.
            Viewer.Document = TextDocument(
                "Не удалось открыть файл справки.",
                "",
                "Путь: " + path,
                "Причина: " + ex.Message,
                "",
                "Файл должен быть в формате RTF. Проверьте, что он не повреждён " +
                "и сохранён как «Текст в формате RTF».");
            SetStatus("Ошибка чтения файла справки", error: true);
        }
    }

    /// Штатное расположение — первым; остальные — если файл положили к конфигурации или рядом с exe.
    private static string? ResolveHelpFile() => new[]
    {
        HelpFilePath,
        Path.Combine(ConfigRepository.DefaultConfigFolder, HelpFileName),
        Path.Combine(AppPaths.BaseFolder, HelpFileName)
    }.FirstOrDefault(File.Exists);

    private void ShowPlaceholder()
    {
        Viewer.Document = TextDocument(
            "Файл справки не найден.",
            "",
            "Поместите файл «" + HelpFileName + "» в папку:",
            HelpFolder,
            "",
            "Требования к файлу:",
            "  • формат RTF (например, сохранённый в Word как «Текст в формате RTF»);",
            "  • текст и изображения размещаются в одном файле;",
            "  • изображения — в формате PNG или JPEG: рисунки, сохранённые как метафайлы",
            "    (WMF/EMF, так их часто сохраняет WordPad), в этом окне не отображаются.",
            "",
            "После добавления файла нажмите «Обновить».");
        SetStatus("Файл справки не найден", error: true);
    }

    private static FlowDocument TextDocument(params string[] lines)
    {
        var p = new Paragraph();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(lines[i]));
        }
        var doc = new FlowDocument(p)
        {
            PagePadding = new Thickness(12),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13.33   // 10 pt, как в WinForms
        };
        return doc;
    }

    private void SetStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = error ? Brushes.Red : (Brush)FindResource("Brush.Hint");
    }

    // ================= ссылки =================
    /// Адреса в обычном тексте превращаются в ссылки (гиперссылки из RTF не трогаются).
    private static void LinkifyUrls(FlowDocument doc)
    {
        var runs = new List<Run>();
        CollectRuns(doc.Blocks, runs);

        foreach (var run in runs)
        {
            var matches = UrlRx.Matches(run.Text);
            if (matches.Count == 0) continue;

            // Позиции вычисляются заранее; ссылки создаются с конца — начало строки не смещается.
            var found = new List<(TextPointer start, TextPointer end, string url)>();
            foreach (Match m in matches)
            {
                string url = m.Value.TrimEnd('.', ',', ';', ':', ')', '!', '?');
                var start = run.ContentStart.GetPositionAtOffset(m.Index);
                var end = run.ContentStart.GetPositionAtOffset(m.Index + url.Length);
                if (start != null && end != null) found.Add((start, end, url));
            }

            for (int i = found.Count - 1; i >= 0; i--)
            {
                var (start, end, url) = found[i];
                string target = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + url : url;
                if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
                    _ = new Hyperlink(start, end) { NavigateUri = uri };
            }
        }
    }

    private static void CollectRuns(IEnumerable<Block> blocks, List<Run> runs)
    {
        foreach (var b in blocks)
        {
            switch (b)
            {
                case Paragraph p: CollectRuns(p.Inlines, runs); break;
                case Section s: CollectRuns(s.Blocks, runs); break;
                case List l: foreach (var li in l.ListItems) CollectRuns(li.Blocks, runs); break;
                case Table t:
                    foreach (var g in t.RowGroups)
                        foreach (var r in g.Rows)
                            foreach (var c in r.Cells) CollectRuns(c.Blocks, runs);
                    break;
            }
        }
    }

    private static void CollectRuns(IEnumerable<Inline> inlines, List<Run> runs)
    {
        foreach (var i in inlines)
        {
            switch (i)
            {
                case Hyperlink: break;                                    // уже ссылка
                case Run r: runs.Add(r); break;
                case Span s: CollectRuns(s.Inlines, runs); break;         // Bold, Italic и т. п.
                case AnchoredBlock a: CollectRuns(a.Blocks, runs); break; // Figure, Floater
            }
        }
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            if (e.Uri != null)
                _shell.OpenUrl(e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.OriginalString);
        }
        catch { /* недоступная ссылка не должна ронять окно справки */ }
        e.Handled = true;
    }

    // ================= кнопки и масштаб =================
    private void OnReload(object sender, RoutedEventArgs e) => LoadContent();

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(HelpFolder);
            _shell.OpenFolder(HelpFolder);
        }
        catch (Exception ex)
        {
            _dialogs.Warning("Не удалось открыть папку:\n" + ex.Message, "Справка");
        }
    }

    private void OnViewerWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        Viewer.Zoom = Math.Clamp(Viewer.Zoom + (e.Delta > 0 ? 10 : -10), Viewer.MinZoom, Viewer.MaxZoom);
        e.Handled = true;
    }
}
