namespace TwdSaveEditor.Season.S1.Chapters;

public sealed record S1EpisodeChapters(
    int Episode,
    IReadOnlyList<S1Chapter> Chapters,
    IReadOnlyList<S1DecisionFlag> DecisionFlags,
    IReadOnlyList<S1DecisionPoint> DecisionPoints)
{
    public S1Chapter? Find(string chapterId) => Chapters.FirstOrDefault(chapter => chapter.Id == chapterId);

    public bool IsDecided(S1DecisionFlag flag, S1Chapter chapter) => IsReached(flag.DecidedFrom, chapter);

    public bool IsDecided(string choiceKey, S1Chapter chapter)
    {
        var point = DecisionPoints.FirstOrDefault(candidate => candidate.ChoiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));
        return point == null || IsReached(point.DecidedFrom, chapter);
    }

    private bool IsReached(string chapterId, S1Chapter chapter)
    {
        var from = IndexOf(chapterId);
        return from >= 0 && IndexOf(chapter.Id) >= from;
    }

    private int IndexOf(string chapterId)
    {
        for (var index = 0; index < Chapters.Count; index++)
        {
            if (Chapters[index].Id == chapterId)
            {
                return index;
            }
        }

        return -1;
    }
}
