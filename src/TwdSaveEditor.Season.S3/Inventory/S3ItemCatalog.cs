using System.Text.Json;

namespace TwdSaveEditor.Season.S3.Inventory;

public static class S3ItemCatalog
{
    private const string ResourceSuffix = "s3.items.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S3ItemData> Data = new(Load);

    public static IReadOnlyList<S3EpisodeItems> All => Data.Value.Episodes;

    public static S3EpisodeItems? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S3ItemData Load()
    {
        var assembly = typeof(S3ItemCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S3ItemData>(stream, JsonOptions) ?? new S3ItemData([]);
    }
}
