using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class InventoryCalls
{
    public const string ItemPrefix = "ui_item_";

    private const string NameSeparator = " - ";

    public static IEnumerable<string> Added(string script) => AddedNames(script).Select(ItemId);

    public static IEnumerable<string> Removed(string script) => Remove().Matches(script).Select(match => ItemId(match.Groups[1].Value));

    public static IEnumerable<string> AddedNames(string script) => Add().Matches(script).Select(match => match.Groups[1].Value);

    public static IEnumerable<string> RemovedNames(string script) =>
        Remove().Matches(script).Where(match => !match.Groups["kept"].Success).Select(match => match.Groups[1].Value);

    public static bool Clears(string script) => Clear().IsMatch(script);

    public static string ItemId(string name) => name.StartsWith(ItemPrefix, StringComparison.Ordinal) ? name : ItemPrefix + name;

    public static IEnumerable<Item> Registered(string script) =>
        Register().Matches(script).Select(match => new Item(
            match.Groups["key"].Value,
            match.Groups["key"].Value,
            match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups["key"].Value[(match.Groups["key"].Value.IndexOf(NameSeparator, StringComparison.Ordinal) + NameSeparator.Length)..]));

    [GeneratedRegex("\\b(?:Inventory_)?InitItem\\(\\s*\"(?<key>[^\"]+)\"(?:\\s*,\\s*(?:\"[^\"]*\"|nil)\\s*,\\s*\"(?<name>[^\"]+)\")?")]
    private static partial Regex Register();

    [GeneratedRegex("\\bInventory_AddItem\\(\\s*\"([^\"]+)\"")]
    private static partial Regex Add();

    [GeneratedRegex("\\bInventory_RemoveItem\\(\\s*\"([^\"]+)\"(?<kept>\\s*,\\s*[1-9])?")]
    private static partial Regex Remove();

    [GeneratedRegex("\\bInventory_Clear\\(\\s*\\)")]
    private static partial Regex Clear();
}
