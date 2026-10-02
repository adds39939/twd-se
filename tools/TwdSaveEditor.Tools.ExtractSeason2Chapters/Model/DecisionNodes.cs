namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record DecisionNodes(string ChoiceKey, int Episode, IReadOnlyList<ulong> Nodes);
