namespace TwdSaveEditor.Core.Model;

public sealed class Property(Symbol keySymbol, PropertyValue value)
{
    public Symbol KeySymbol { get; } = keySymbol;
    public PropertyValue Value { get; set; } = value;
}
