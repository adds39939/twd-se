namespace TwdSaveEditor.Core.Model;

public sealed class FloatValue(float value) : PropertyValue
{
    public float Value { get; set; } = value;
    public override string TypeName => "float";
    public override object BoxedValue => Value;
}
