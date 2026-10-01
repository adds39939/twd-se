using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Model;

public readonly record struct Symbol(ulong Value)
{
    public static readonly Symbol Empty = new(0);

    public static Symbol FromString(string name) => new(TelltaleHash.ComputeCrc64(name));

    public override string ToString() => $"Symbol(0x{Value:X16})";
}
