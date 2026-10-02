namespace TwdSaveEditor.Tools.ExtractDecisions.Model;

public sealed class DecisionRow
{
    public required int Episode { get; init; }

    public required string Key { get; init; }

    public required string Description { get; init; }

    public required List<RowOption> Options { get; init; }

    public bool Story { get; set; }
}
