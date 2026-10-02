namespace TwdSaveEditor.Tests.Common.Data;

public static class TestDataHelper
{
    private static readonly string TestDataDir = FindTestDataDir();

    private static string FindTestDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "TestData");
            if (Directory.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir.FullName, "TestData");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find tests/TestData directory");
    }

    public static string GetPath(string season, string fileName)
        => Path.Combine(TestDataDir, season, fileName);

    public static string GetSeasonDir(string season)
        => Path.Combine(TestDataDir, season);

    public static bool Exists(string season, string fileName)
        => File.Exists(GetPath(season, fileName));

    public static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "twd_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
