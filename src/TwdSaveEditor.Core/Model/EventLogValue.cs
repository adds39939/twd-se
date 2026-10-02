namespace TwdSaveEditor.Core.Model;

public readonly record struct EventLogValue(byte Kind, ulong Raw, byte Severity)
{
    public const byte SymbolKind = 0;
    public const byte IntegerKind = 1;
    public const byte DoubleKind = 2;

    public bool IsSymbol => Kind == SymbolKind;

    public static EventLogValue Symbol(ulong symbol, byte severity) => new(SymbolKind, symbol, severity);

    public double? Number => Kind switch
    {
        IntegerKind => unchecked((long)Raw),
        DoubleKind => BitConverter.UInt64BitsToDouble(Raw),
        _ => null,
    };

    public static EventLogValue Double(double value, byte severity) => new(DoubleKind, BitConverter.DoubleToUInt64Bits(value), severity);
}
