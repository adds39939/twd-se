namespace TwdSaveEditor.Tools.Common.Hashing;

public static class TelltaleCrc64
{
    private const ulong Polynomial = 0x42F0E1EBA9EA3693;

    private static readonly ulong[] Table = BuildTable();

    public static ulong Compute(string text)
    {
        ulong crc = 0;
        foreach (var c in text.ToLowerInvariant())
            crc = Table[(c ^ (crc >> 56)) & 0xFF] ^ (crc << 8);

        return crc;
    }

    public static ulong Compute(ReadOnlySpan<byte> data)
    {
        ulong crc = 0;
        foreach (var b in data)
            crc = Table[((crc >> 56) ^ b) & 0xFF] ^ (crc << 8);

        return crc;
    }

    private static ulong[] BuildTable()
    {
        var table = new ulong[256];
        for (var i = 0; i < table.Length; i++)
        {
            var crc = (ulong)i << 56;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & (1UL << 63)) != 0 ? (crc << 1) ^ Polynomial : crc << 1;

            table[i] = crc;
        }

        return table;
    }
}
