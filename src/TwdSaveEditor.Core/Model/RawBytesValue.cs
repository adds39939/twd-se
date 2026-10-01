namespace TwdSaveEditor.Core.Model;

public sealed class RawBytesValue(byte[] data, Symbol typeSymbol) : PropertyValue
{
    public byte[] Data { get; set; } = data;
    public Symbol TypeSymbol { get; } = typeSymbol;
    public override string TypeName => $"Raw(0x{TypeSymbol.Value:X16})";
    public override object BoxedValue => Data;
}
