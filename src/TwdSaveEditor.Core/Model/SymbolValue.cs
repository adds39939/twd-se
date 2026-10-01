namespace TwdSaveEditor.Core.Model;

public sealed class SymbolValue(Symbol value) : PropertyValue
{
    public Symbol Value { get; set; } = value;
    public override string TypeName => "Symbol";
    public override object BoxedValue => Value;
}
