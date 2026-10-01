using System.Text;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Bundles;
using TwdSaveEditor.Tools.Common.Compression;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.ValidateEditedSaves.Model;

namespace TwdSaveEditor.Tools.ValidateEditedSaves.Bundles;

public static class BundleInspector
{
    private const int MaxFiles = 500;

    public static BundleInfo Inspect(string path)
    {
        var data = File.ReadAllBytes(path);
        var defaultSize = MetaStreamParser.SectionSize(Bytes.U32(data, 4));
        var debugSize = MetaStreamParser.SectionSize(Bytes.U32(data, 8));
        var asyncField = Bytes.U32(data, 12);
        var asyncCompressed = MetaStreamParser.IsCompressed(asyncField);
        var headerEnd = 20 + Bytes.U32(data, 16) * 12L;
        var asyncStart = headerEnd + defaultSize + debugSize;

        var debug = Bytes.Slice(data, headerEnd + defaultSize, asyncStart);
        var asyncData = ReadAsyncSection(Bytes.Slice(data, asyncStart, asyncStart + MetaStreamParser.SectionSize(asyncField)), asyncCompressed);

        var table = Bytes.Slice(data, headerEnd, headerEnd + defaultSize);
        var entries = ReadEntries(table);

        var files = new Dictionary<string, InnerFile>();
        foreach (var entry in entries)
        {
            if (InspectInnerFile(asyncData, entry) is { } file)
                files[entry.Name] = file;
        }

        return new BundleInfo(
            data.Length,
            debugSize,
            debugSize > 0 ? Bytes.Hex(debug) : "(none)",
            asyncCompressed,
            Bytes.U32(table, 4),
            entries,
            files);
    }

    private static byte[] ReadAsyncSection(ReadOnlySpan<byte> raw, bool compressed)
    {
        if (!compressed)
            return raw.ToArray();
        if (Ttcz.HasMagic(raw))
            return Ttcz.Decompress(raw);

        return Zlib.TryInflate(raw, Zlib.ZlibWindow) ?? throw new InvalidDataException("Async section is not valid zlib data.");
    }

    private static List<BundleEntry> ReadEntries(ReadOnlySpan<byte> table)
    {
        var fileCount = Bytes.U32(table, 4);
        var position = 8;
        var entries = new List<BundleEntry>();
        for (var i = 0; i < Math.Min(fileCount, MaxFiles); i++)
        {
            if (position + 8 > table.Length)
                break;

            var offset = Bytes.U32(table, position);
            var size = Bytes.U32(table, position + 4);
            position += 8;

            var nameStart = position;
            while (position < table.Length && table[position] != 0)
                position++;

            var nameBytes = table[nameStart..position];
            var readable = nameBytes.Length > 0 && !nameBytes.ContainsAnyExceptInRange((byte)0x20, (byte)0x7E);
            var name = readable ? Encoding.ASCII.GetString(nameBytes) : $"_hash_{i}";

            var nameLength = nameBytes.Length + 1;
            position += 1 + ((nameLength + 3) & ~3) - nameLength + 16;
            entries.Add(new BundleEntry(name, offset, size));
        }

        return entries;
    }

    private static InnerFile? InspectInnerFile(byte[] asyncData, BundleEntry entry)
    {
        if ((long)entry.Offset + entry.Size > asyncData.Length)
            return null;

        var inner = asyncData.AsSpan((int)entry.Offset, (int)entry.Size);
        if (inner.Length < 20 || Bytes.U32(inner, 0) is not (MetaStreamParser.MagicMsv6 or MetaStreamParser.MagicMsv5))
            return null;

        var defaultSize = MetaStreamParser.SectionSize(Bytes.U32(inner, 4));
        var debugSize = MetaStreamParser.SectionSize(Bytes.U32(inner, 8));
        var asyncSize = MetaStreamParser.SectionSize(Bytes.U32(inner, 12));
        var versions = Bytes.U32(inner, 16);
        var definitionStart = 20 + versions * 12L;

        var debugStart = definitionStart + defaultSize;
        var debugHex = debugSize > 0 && debugStart + debugSize <= inner.Length
            ? Bytes.Hex(inner.Slice((int)debugStart, (int)debugSize))
            : null;

        PropertySetHeader? propertySet = null;
        OrderedDictionary<string, object>? properties = null;
        if (entry.Name.Contains("metadata") && definitionStart + 12 <= inner.Length)
        {
            propertySet = new PropertySetHeader(
                Bytes.U32(inner, definitionStart),
                Bytes.U32(inner, definitionStart + 4),
                Bytes.U32(inner, definitionStart + 8));
            properties = MetadataProperties.TryParse(inner, definitionStart + 12);
        }

        return new InnerFile(defaultSize, debugSize, asyncSize, versions, debugHex, propertySet, properties);
    }
}
