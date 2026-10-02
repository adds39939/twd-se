using System.Text.Json;

namespace TwdSaveEditor.Season.S1.Chapters;

public static class S1ChapterCatalog
{
    private const string ResourceSuffix = "s1.chapters.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<S1EpisodeChapters>> Episodes = new(Load);

    public static IReadOnlyList<S1EpisodeChapters> All => Episodes.Value;

    public static S1EpisodeChapters? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static IReadOnlyList<S1EpisodeChapters> Load()
    {
        var assembly = typeof(S1ChapterCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<List<S1EpisodeChapters>>(stream, JsonOptions) ?? [];
    }
}
