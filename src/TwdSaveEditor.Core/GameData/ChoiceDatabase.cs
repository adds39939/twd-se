using System.Text.Json;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Database of all known player choices across TWD: The Telltale Definitive Series.
/// Choice definitions are loaded from embedded JSON resource files in the ChoiceData folder.
/// </summary>
public static class ChoiceDatabase
{
    private static readonly Lazy<IReadOnlyList<ChoiceDefinition>> _allChoices = new(LoadAll);

    public static IReadOnlyList<ChoiceDefinition> AllChoices => _allChoices.Value;

    public static IEnumerable<ChoiceDefinition> ForSeason(string seasonKey)
        => AllChoices.Where(c => c.SeasonKey.Equals(seasonKey, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ChoiceDefinition> ForEpisode(string seasonKey, int episode)
        => ForSeason(seasonKey).Where(c => c.Episode == episode);

    private static IReadOnlyList<ChoiceDefinition> LoadAll()
    {
        var assembly = typeof(ChoiceDatabase).Assembly;
        var choices = new List<ChoiceDefinition>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".json") || !resourceName.Contains("ChoiceData"))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            var defs = JsonSerializer.Deserialize<List<ChoiceDefinition>>(stream, JsonOptions);
            if (defs != null) choices.AddRange(defs);
        }

        return choices.AsReadOnly();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
