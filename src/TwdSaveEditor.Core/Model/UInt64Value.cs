namespace TwdSaveEditor.Core.Model;

public sealed class UInt64Value(ulong value) : PropertyValue
{
    public ulong Value { get; set; } = value;
    public override string TypeName => "uint64";
    public override object BoxedValue => Value;
}
