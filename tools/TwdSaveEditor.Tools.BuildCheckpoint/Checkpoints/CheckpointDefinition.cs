namespace TwdSaveEditor.Tools.BuildCheckpoint.Checkpoints;

public sealed record CheckpointDefinition(
    int PersistentEpisode,
    string EpisodeId,
    string Script,
    string ChapterId,
    string DialogItem,
    string DialogFile,
    string DialogNode,
    IReadOnlyList<ulong> ResourceSets);
