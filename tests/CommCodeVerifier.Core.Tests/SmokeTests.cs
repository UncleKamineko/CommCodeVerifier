using Xunit;

namespace CommCodeVerifier.Core.Tests;

public class SmokeTests
{
    private static string DataPath(string folder) =>
        Path.Combine(AppContext.BaseDirectory, folder);

    [Fact]
    public void ConfigSnapshotIsCopied()
    {
        string dir = DataPath("Config");
        Assert.True(Directory.Exists(dir), $"Нет папки {dir}");
        Assert.True(File.Exists(Path.Combine(dir, "Значащие слова.txt")), "Нет «Значащие слова.txt»");
    }

    [Fact]
    public void GoldenDataIsCopied()
    {
        string dir = DataPath("Golden");
        Assert.True(Directory.Exists(dir), $"Нет папки {dir}");
        Assert.True(File.Exists(Path.Combine(dir, "settings.json")), "Нет settings.json эталона");
    }
}
