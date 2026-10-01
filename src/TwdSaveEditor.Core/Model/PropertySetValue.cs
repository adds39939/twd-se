namespace TwdSaveEditor.Core.Model;

public sealed class PropertySetValue(PropertySet value) : PropertyValue
{
    public PropertySet Value { get; set; } = value;
    public override string TypeName => "PropertySet";
    public override object BoxedValue => Value;
}
