using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Items;

public static partial class ItemRegistryReader
{
    private const string AgentConstant = "kInventoryLogicAgent";
    private const string FlagOverride = "ShouldDisplay";
    private const string NameSeparator = " - ";

    public static List<ItemDefinition> Read(string episodeScript)
    {
        var body = Registry().Match(episodeScript).Groups[1].Value;
        return
        [
            .. Registration().Matches(body).Select(match =>
            {
                var agent = match.Groups["agent"].Value == AgentConstant ? DebugMenuReader.InventoryAgent : match.Groups["agent"].Value.Trim('"');
                return new ItemDefinition(agent, match.Groups["key"].Value, Name(agent, match.Groups["key"].Value), match.Groups["override"].Value == FlagOverride, 1);
            }),
        ];
    }

    private static string Name(string agent, string key)
    {
        var separator = key.IndexOf(NameSeparator, StringComparison.Ordinal);
        var text = separator < 0 ? key : key[(separator + NameSeparator.Length)..];
        if (agent == DebugMenuReader.InventoryAgent)
        {
            return Words().Replace(text, " $1");
        }

        text = Words().Replace(Possession().Replace(text, string.Empty), " $1");
        text = char.ToUpperInvariant(text[0]) + text[1..];
        return separator < 0 ? text : $"{text} ({key[..separator]})";
    }

    [GeneratedRegex("^function WDEpisode_InventoryInit\\(\\)\\n(.*?)^end$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Registry();

    [GeneratedRegex("AddInventoryItem\\((?<agent>\\w+|\"[^\"]+\"), \"(?<key>[^\"]+)\", \"[^\"]*\"\\)(?:\\.(?<override>\\w+) = function)?")]
    private static partial Regex Registration();

    [GeneratedRegex("^(?:got|have|player)\\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Possession();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex Words();
}
