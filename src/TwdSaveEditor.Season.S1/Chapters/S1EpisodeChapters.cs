namespace TwdSaveEditor.Season.S1.Chapters;

public sealed record S1EpisodeChapters(int Episode, IReadOnlyList<S1Chapter> Chapters, IReadOnlyList<S1DecisionFlag> DecisionFlags)
{
    public S1Chapter? Find(string chapterId) => Chapters.FirstOrDefault(chapter => chapter.Id == chapterId);

    public bool IsDecided(S1DecisionFlag flag, S1Chapter chapter)
    {
        var from = Chapters.ToList().FindIndex(candidate => candidate.Id == flag.DecidedFrom);
        return from >= 0 && Chapters.ToList().IndexOf(chapter) >= from;
    }
}
