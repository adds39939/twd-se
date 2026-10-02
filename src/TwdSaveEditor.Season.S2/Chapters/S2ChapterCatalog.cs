using System.Text.Json;

namespace TwdSaveEditor.Season.S2.Chapters;

public static class S2ChapterCatalog
{
    private const string ResourceSuffix = "s2.chapters.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S2ChapterData> Data = new(Load);

    public static IReadOnlyList<S2EpisodeChapters> All => Data.Value.Episodes;

    public static IReadOnlyList<S2ImportedKeys> ImportedKeys => Data.Value.ImportedKeys;

    public static S2EpisodeChapters? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S2ChapterData Load()
    {
        var assembly = typeof(S2ChapterCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S2ChapterData>(stream, JsonOptions) ?? new S2ChapterData([], []);
    }
}
