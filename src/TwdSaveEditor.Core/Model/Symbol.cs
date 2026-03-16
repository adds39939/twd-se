using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Model;

/// <summary>
/// A Telltale symbol — a CRC64 hash that identifies property names and types.
/// </summary>
public readonly record struct Symbol(ulong Value)
{
    public static readonly Symbol Empty = new(0);

    public static Symbol FromString(string name) => new(TelltaleHash.ComputeCrc64(name));

    public override string ToString() => $"Symbol(0x{Value:X16})";
}
