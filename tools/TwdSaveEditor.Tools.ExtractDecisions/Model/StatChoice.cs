namespace TwdSaveEditor.Tools.ExtractDecisions.Model;

public sealed record StatChoice(int Episode, int Sequence, string Description, string Category, List<(string Text, List<string> Nodes, string Expression)> Options);
