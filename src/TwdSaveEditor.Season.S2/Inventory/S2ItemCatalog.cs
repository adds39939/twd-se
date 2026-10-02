using System.Text.Json;

namespace TwdSaveEditor.Season.S2.Inventory;

public static class S2ItemCatalog
{
    private const string ResourceSuffix = "s2.items.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S2ItemData> Data = new(Load);

    public static IReadOnlyList<S2EpisodeItems> All => Data.Value.Episodes;

    public static S2EpisodeItems? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S2ItemData Load()
    {
        var assembly = typeof(S2ItemCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S2ItemData>(stream, JsonOptions) ?? new S2ItemData([]);
    }
}
