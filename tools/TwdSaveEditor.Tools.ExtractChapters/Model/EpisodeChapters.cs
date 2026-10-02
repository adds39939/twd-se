namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record EpisodeChapters(
    int Episode,
    IReadOnlyList<SceneScript> Scripts,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SceneDialogs,
    IReadOnlyList<Chapter> Chapters,
    IReadOnlyList<string> Toggles,
    IReadOnlyList<SceneTransition> Transitions,
    IReadOnlyList<CheckpointCall> Checkpoints,
    IReadOnlyList<Decision> Decisions,
    IReadOnlyList<StoryAction> StoryActions);
