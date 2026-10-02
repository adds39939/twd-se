namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Model;

public sealed record StatChoice(int Episode, int Sequence, string Description, string Category, List<(string Text, List<string> Nodes)> Options);
