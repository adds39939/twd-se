using TwdSaveEditor.Tools.Common.Compression;
using TwdSaveEditor.Tools.Common.Cryptography;

namespace TwdSaveEditor.Tools.ExtractKey.Keys;

public sealed class KeyLocator(byte[] exe, TextWriter output)
{
    public const int KnownOffset = 0xC3D7A0;
    public const int KeyLength = 55;

    public ReadOnlySpan<byte> KeyAt(int offset) => exe.AsSpan(offset, KeyLength);

    public bool HasKeyShape(int offset)
    {
        var end = offset + KeyLength;
        return exe.Length > end && exe[end] == 0 && !KeyAt(offset).Contains((byte)0);
    }

    public int? FindVerified(ArchiveProbe probe)
    {
        if (HasKeyShape(KnownOffset) && Decrypts(KeyAt(KnownOffset), probe))
        {
            return KnownOffset;
        }

        var offsets = ScanOffsets();
        output.WriteLine($"Key is not at 0x{KnownOffset:X}; scanning {offsets.Count} candidates in {GameLocations.ExeName}...");
        foreach (var offset in offsets)
        {
            if (Decrypts(KeyAt(offset), probe))
            {
                return offset;
            }
        }

        return null;
    }

    private List<int> ScanOffsets()
    {
        var offsets = new List<int>();
        var runStart = 0;
        for (var i = 0; i < exe.Length; i++)
        {
            if (exe[i] != 0)
            {
                continue;
            }

            if (i - runStart >= KeyLength && i - KeyLength != KnownOffset)
            {
                offsets.Add(i - KeyLength);
            }

            runStart = i + 1;
        }

        return offsets.OrderBy(candidate => Math.Abs(candidate - KnownOffset)).ToList();
    }

    private static bool Decrypts(ReadOnlySpan<byte> key, ArchiveProbe probe)
    {
        var plain = new BlowfishV7(key).DecryptData(probe.Data);
        if (!probe.Compressed)
        {
            return HasPlaintextMagic(plain);
        }

        return HasPlaintextMagic(Zlib.InflatePrefix(plain, Zlib.ZlibWindow, 4))
            || HasPlaintextMagic(Zlib.InflatePrefix(plain, Zlib.RawWindow, 4));
    }

    private static bool HasPlaintextMagic(ReadOnlySpan<byte> data) => data.StartsWith("4ATT"u8) || data.StartsWith("3ATT"u8);
}
