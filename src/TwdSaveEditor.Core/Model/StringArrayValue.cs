namespace TwdSaveEditor.Core.Model;

public sealed class StringArrayValue(List<string> values) : PropertyValue
{
    public List<string> Values { get; set; } = values;
    public override string TypeName => "DCArray<String>";
    public override object BoxedValue => string.Join(", ", Values);
}
