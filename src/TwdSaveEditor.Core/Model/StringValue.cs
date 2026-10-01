namespace TwdSaveEditor.Core.Model;

public sealed class StringValue(string value) : PropertyValue
{
    public string Value { get; set; } = value;
    public override string TypeName => "String";
    public override object BoxedValue => Value;
}
