namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record DecisionNodes(string ChoiceKey, int Episode, IReadOnlyList<ulong> Nodes);
