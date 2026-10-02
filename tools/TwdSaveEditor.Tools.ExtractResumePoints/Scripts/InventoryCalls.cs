using System.Text.RegularExpressions;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class InventoryCalls
{
    public const string ItemPrefix = "ui_item_";

    public static IEnumerable<string> Added(string script) => Add().Matches(script).Select(ItemId);

    public static IEnumerable<string> Removed(string script) => Remove().Matches(script).Select(ItemId);

    public static bool Clears(string script) => Clear().IsMatch(script);

    public static string ItemId(string name) => name.StartsWith(ItemPrefix, StringComparison.Ordinal) ? name : ItemPrefix + name;

    private static string ItemId(Match match) => ItemId(match.Groups[1].Value);

    [GeneratedRegex("\\bInventory_AddItem\\(\\s*\"(\\w+)\"")]
    private static partial Regex Add();

    [GeneratedRegex("\\bInventory_RemoveItem\\(\\s*\"(\\w+)\"")]
    private static partial Regex Remove();

    [GeneratedRegex("\\bInventory_Clear\\(\\s*\\)")]
    private static partial Regex Clear();
}
