namespace TwdSaveEditor.Season.S3.Chapters;

public sealed record S3EpisodeChapters(int Episode, IReadOnlyList<S3Chapter> Chapters)
{
    public S3Chapter? Find(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
