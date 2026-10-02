namespace TwdSaveEditor.Tools.ExtractDecisions.Model;

public sealed record LogicKey(string Name, bool IsMap, List<(string Value, List<string> Nodes, string Expression)> Values, int ReadFrom);
