using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Compression;

namespace TwdSaveEditor.Tools.Common.MetaStreams;

public static class MetaStreamParser
{
    public const uint MagicMsv5 = 0x4D535635;
    public const uint MagicMsv6 = 0x4D535636;

    private const uint CompressedFlag = 0x80000000;
    private const int HeaderSize = 20;
    private const int VersionEntrySize = 12;

    public static bool IsCompressed(uint sizeField) => (sizeField & CompressedFlag) != 0;

    public static uint SectionSize(uint sizeField) => sizeField & ~CompressedFlag;

    public static string MagicName(uint magic) => magic switch
    {
        MagicMsv5 => "MSV5",
        MagicMsv6 => "MSV6",
        _ => $"0x{magic:X8}",
    };

    public static List<VersionEntry> ReadVersionEntries(ReadOnlySpan<byte> data, ref long position, uint count)
    {
        var entries = new List<VersionEntry>();
        for (uint i = 0; i < count; i++)
        {
            entries.Add(new VersionEntry(Bytes.U64(data, position), Bytes.U32(data, position + 8)));
            position += VersionEntrySize;
        }

        return entries;
    }

    public static MetaStreamSections? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            return null;
        }

        var magic = Bytes.U32(data, 0);
        if (magic is not (MagicMsv5 or MagicMsv6))
        {
            return null;
        }

        uint[] sizeFields = [Bytes.U32(data, 4), Bytes.U32(data, 8), Bytes.U32(data, 12)];

        long position = HeaderSize;
        var versionEntries = ReadVersionEntries(data, ref position, Bytes.U32(data, 16));

        var sections = new byte[sizeFields.Length][];
        for (var i = 0; i < sizeFields.Length; i++)
        {
            var size = SectionSize(sizeFields[i]);
            if (size == 0 || position + size > data.Length)
            {
                sections[i] = [];
                continue;
            }

            var raw = data.Slice((int)position, (int)size);
            position += size;
            sections[i] = IsCompressed(sizeFields[i]) ? Inflate(raw) : raw.ToArray();
        }

        return new MetaStreamSections(magic, versionEntries, sections[0], sections[1], sections[2]);
    }

    private static byte[] Inflate(ReadOnlySpan<byte> raw) =>
        Ttcz.HasMagic(raw)
            ? Ttcz.Decompress(raw)
            : Zlib.TryInflateAny(raw, Zlib.ZlibWindow, Zlib.RawWindow) ?? raw.ToArray();
}
