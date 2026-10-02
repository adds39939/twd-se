namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogLogicGroup(int Operator, int GroupOperator, IReadOnlyList<DialogLogicEntry> Entries, IReadOnlyList<DialogLogicGroup> Groups)
{
    public const int And = 1;
    public const int Or = 2;

    public bool IsEmpty => Entries.Count == 0 && Groups.All(group => group.IsEmpty);

    public IEnumerable<DialogLogicEntry> AllEntries => Entries.Concat(Groups.SelectMany(group => group.AllEntries));
}
