namespace TwdSaveEditor.Core.Model;

public sealed class BoolValue(bool value) : PropertyValue
{
    public bool Value { get; set; } = value;
    public override string TypeName => "bool";
    public override object BoxedValue => Value;
}
