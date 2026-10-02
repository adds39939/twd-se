namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Model;

public sealed record LogicKey(string Name, bool IsMap, List<(string Value, List<string> Nodes)> Values);
