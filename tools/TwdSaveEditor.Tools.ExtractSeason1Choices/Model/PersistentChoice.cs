namespace TwdSaveEditor.Tools.ExtractSeason1Choices.Model;

public sealed record PersistentChoice(int Episode, string Key, string? Description, List<PersistentOption> Options);
