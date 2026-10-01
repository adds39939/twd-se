namespace TwdSaveEditor.Season.Common.Model;

public sealed class ChoiceDefinition
{
    public required string SeasonKey { get; init; }

    public required int Episode { get; init; }

    public required string Description { get; init; }

    public required string ChoiceKey { get; init; }

    public required ChoiceOption[] Options { get; init; }

    public string Category { get; init; } = "Major";
}
