namespace TwdSaveEditor.Tools.ExtractDecisions.Model;

public sealed class DecisionOption
{
    public List<string> Nodes { get; } = [];

    public string? Label { get; set; }

    public string? LogicValue { get; set; }
}
