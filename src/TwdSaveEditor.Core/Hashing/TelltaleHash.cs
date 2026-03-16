using System.IO.Hashing;
using System.Text;

namespace TwdSaveEditor.Core.Hashing;

/// <summary>
/// CRC64 ECMA-182 hash used by Telltale for symbol names.
/// All inputs are lowercased before hashing to match Telltale's case-insensitive behavior.
/// </summary>
public static class TelltaleHash
{
    public static ulong ComputeCrc64(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input.ToLowerInvariant());
        return Crc64.HashToUInt64(bytes);
    }

    public static ulong ComputeCrc64(ReadOnlySpan<byte> data)
    {
        return Crc64.HashToUInt64(data);
    }
}
