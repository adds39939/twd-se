using System.Text.Json;

namespace TwdSaveEditor.Season.S1.Inventory;

public static class S1ItemCatalog
{
    private const string ResourceSuffix = "s1.items.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S1ItemData> Data = new(Load);

    public static IReadOnlyList<S1EpisodeItems> All => Data.Value.Episodes;

    public static S1EpisodeItems? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S1ItemData Load()
    {
        var assembly = typeof(S1ItemCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S1ItemData>(stream, JsonOptions) ?? new S1ItemData([]);
    }
}
