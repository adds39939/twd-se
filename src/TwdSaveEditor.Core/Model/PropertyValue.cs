namespace TwdSaveEditor.Core.Model;

/// <summary>
/// Base class for all property values in a Telltale PropertySet.
/// </summary>
public abstract class PropertyValue
{
    public abstract string TypeName { get; }
    public abstract object BoxedValue { get; }
}

public sealed class BoolValue(bool value) : PropertyValue
{
    public bool Value { get; set; } = value;
    public override string TypeName => "bool";
    public override object BoxedValue => Value;
}

public sealed class IntValue(int value) : PropertyValue
{
    public int Value { get; set; } = value;
    public override string TypeName => "int";
    public override object BoxedValue => Value;
}

public sealed class FloatValue(float value) : PropertyValue
{
    public float Value { get; set; } = value;
    public override string TypeName => "float";
    public override object BoxedValue => Value;
}

public sealed class UInt64Value(ulong value) : PropertyValue
{
    public ulong Value { get; set; } = value;
    public override string TypeName => "uint64";
    public override object BoxedValue => Value;
}

public sealed class StringValue(string value) : PropertyValue
{
    public string Value { get; set; } = value;
    public override string TypeName => "String";
    public override object BoxedValue => Value;
}

public sealed class SymbolValue(Symbol value) : PropertyValue
{
    public Symbol Value { get; set; } = value;
    public override string TypeName => "Symbol";
    public override object BoxedValue => Value;
}

public sealed class PropertySetValue(PropertySet value) : PropertyValue
{
    public PropertySet Value { get; set; } = value;
    public override string TypeName => "PropertySet";
    public override object BoxedValue => Value;
}

/// <summary>
/// Fallback for unknown/unhandled types — preserves raw bytes for lossless round-tripping.
/// </summary>
public sealed class RawBytesValue(byte[] data, Symbol typeSymbol) : PropertyValue
{
    public byte[] Data { get; set; } = data;
    public Symbol TypeSymbol { get; } = typeSymbol;
    public override string TypeName => $"Raw(0x{TypeSymbol.Value:X16})";
    public override object BoxedValue => Data;
}
