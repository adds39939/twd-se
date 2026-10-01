using System.Numerics;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.Model;

public sealed record Expression(string Type, string? Guid = null, List<string>? Guids = null, BigInteger? Decimal = null)
{
    public bool IsDecimal => Type == ExpressionKind.Decimal;

    public string HexHash => Decimal is { } value && value <= ulong.MaxValue
        ? $"0x{(ulong)value:X16}"
        : $"0x{Decimal?.ToString("X").TrimStart('0')}";
}
