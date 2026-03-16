namespace TwdSaveEditor.Core.Model;

/// <summary>
/// A single key-value property within a PropertySet.
/// </summary>
public sealed class Property(Symbol keySymbol, PropertyValue value)
{
    public Symbol KeySymbol { get; } = keySymbol;
    public PropertyValue Value { get; set; } = value;
}
