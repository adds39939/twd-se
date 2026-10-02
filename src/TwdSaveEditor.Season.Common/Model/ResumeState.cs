namespace TwdSaveEditor.Season.Common.Model;

public sealed record ResumeState(int Episode, string? Checkpoint, string? SavedAt, bool CheckpointDamaged = false, bool SeasonFinished = false)
{
    public bool StartsFromBeginning => Checkpoint == null && !CheckpointDamaged && !SeasonFinished;
}
