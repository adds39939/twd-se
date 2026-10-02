namespace TwdSaveEditor.Season.S2.Chapters;

public sealed record S2EpisodeChapters(int Episode, IReadOnlyList<S2Chapter> Chapters)
{
    public S2Chapter Opening => Chapters.First(chapter => chapter.StartsEpisode);

    public S2Chapter? Find(string id) =>
        Chapters.FirstOrDefault(chapter => chapter.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
