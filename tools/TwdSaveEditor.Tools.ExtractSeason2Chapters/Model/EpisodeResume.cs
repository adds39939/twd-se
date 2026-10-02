namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record EpisodeResume(int Episode, IReadOnlyList<GameChapter> Chapters, IReadOnlyList<ResumePoint> Points, IReadOnlyList<string> Unplaced);
