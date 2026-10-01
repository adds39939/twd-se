using TwdSaveEditor.Tools.Common.Configuration;

namespace TwdSaveEditor.Tools.ExtractKey.Keys;

public static class GameLocations
{
    public const string ExeName = "WDC.exe";
    public const string ArchivesDirectoryName = "Archives";

    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static string? FindExe(string path)
    {
        path = Normalize(path);
        string[] candidates = [path, Path.Combine(path, ExeName), Path.Combine(Path.GetDirectoryName(path) ?? path, ExeName)];
        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? FindArchivesDirectory(string exePath)
    {
        string?[] candidates =
        [
            Path.Combine(Path.GetDirectoryName(exePath) ?? "", ArchivesDirectoryName),
            ToolPaths.Argument([], 0, ToolPaths.ArchivesVariable),
        ];
        return candidates.FirstOrDefault(Directory.Exists);
    }
}
