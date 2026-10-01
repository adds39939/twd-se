namespace TwdSaveEditor.Core.Model;

public sealed class IntValue(int value) : PropertyValue
{
    public int Value { get; set; } = value;
    public override string TypeName => "int";
    public override object BoxedValue => Value;
}
