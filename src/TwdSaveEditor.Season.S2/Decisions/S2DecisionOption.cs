namespace TwdSaveEditor.Season.S2.Decisions;

public sealed record S2DecisionOption(string Value, string? LogicValue, IReadOnlyList<string> Nodes);
