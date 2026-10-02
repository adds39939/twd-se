using System.Text.Json;

namespace TwdSaveEditor.Season.S3.Chapters;

public static class S3ChapterCatalog
{
    private const string ResourceSuffix = "s3.chapters.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S3ChapterData> Data = new(Load);

    public static IReadOnlyList<S3EpisodeChapters> All => Data.Value.Episodes;

    public static S3EpisodeChapters? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S3ChapterData Load()
    {
        var assembly = typeof(S3ChapterCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S3ChapterData>(stream, JsonOptions) ?? new S3ChapterData([]);
    }
}
