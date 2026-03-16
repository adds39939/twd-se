namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Defines a player choice/decision in the game.
/// In the Definitive Series, choices are stored as string entries in choices.prop
/// using the format "key_name - value" inside a DCArray of (String, bool) pairs.
/// </summary>
public sealed class ChoiceDefinition
{
    /// <summary>Season key (e.g., "s1", "s2", "michonne").</summary>
    public required string SeasonKey { get; init; }

    /// <summary>Episode number within the season.</summary>
    public required int Episode { get; init; }

    /// <summary>Human-readable description of the choice moment.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The choice key in the save data (e.g., "dougcarley_saved").
    /// This appears before " - " in the choices.prop string entry.
    /// </summary>
    public required string ChoiceKey { get; init; }

    /// <summary>
    /// The possible outcomes with their display names and string values.
    /// </summary>
    public required ChoiceOption[] Options { get; init; }

    /// <summary>Category tag for UI grouping.</summary>
    public string Category { get; init; } = "Major";
}

/// <summary>
/// One possible outcome of a choice.
/// </summary>
public sealed class ChoiceOption
{
    /// <summary>Display label (e.g., "Saved Doug", "Sided with Kenny").</summary>
    public required string Label { get; init; }

    /// <summary>
    /// The string value stored in the save data after " - ".
    /// For example: "carley", "true", "false", "duck", "7".
    /// </summary>
    public required string Value { get; init; }
}
