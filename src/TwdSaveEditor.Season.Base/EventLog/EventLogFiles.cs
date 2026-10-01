namespace TwdSaveEditor.Season.Base.EventLog;

internal static class EventLogFiles
{
    public const string EStoreExtension = ".estore";
    public const string EPageExtension = ".epage";

    public static string GetSlotBaseName(string bundleFileName)
        => $"_{Path.GetFileNameWithoutExtension(bundleFileName)}";

    public static string GetEStoreName(string bundleFileName)
        => $"{GetSlotBaseName(bundleFileName)}_id{EStoreExtension}";

    public static string GetEPagePrefix(string bundleFileName)
        => $"{GetSlotBaseName(bundleFileName)}_id_Page";

    public static int ExtractPageNumber(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var pageIdx = name.LastIndexOf("Page", StringComparison.Ordinal);
        if (pageIdx < 0) return 0;
        var numStr = name[(pageIdx + 4)..];
        return int.TryParse(numStr, out var num) ? num : 0;
    }
}
