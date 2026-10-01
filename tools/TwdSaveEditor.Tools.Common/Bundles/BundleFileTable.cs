using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Bundles;

public static class BundleFileTable
{
    private const uint MaxFiles = 1_000_000;
    private const int NameSize = 16;
    private const int SymbolsSize = 16;

    public static List<BundleEntry> Parse(ReadOnlySpan<byte> table)
    {
        var entries = new List<BundleEntry>();
        if (table.Length < 8)
            return entries;

        var fileCount = Bytes.U32(table, 4);
        if (fileCount > MaxFiles)
            return entries;

        var position = 8;
        for (uint i = 0; i < fileCount; i++)
        {
            if (position + 8 > table.Length)
                break;

            var offset = Bytes.U32(table, position);
            var size = Bytes.U32(table, position + 4);
            position += 8;

            if (position + NameSize + SymbolsSize > table.Length)
                break;

            var name = table.Slice(position, NameSize);
            var nameEnd = name.IndexOf((byte)0);
            position += NameSize + SymbolsSize;
            entries.Add(new BundleEntry(TextFormat.DecodeAscii(nameEnd < 0 ? name : name[..nameEnd]), offset, size));
        }

        return entries;
    }

    public static bool Contains(MetaStreamSections bundle, BundleEntry entry) =>
        (long)entry.Offset + entry.Size <= bundle.Async.Length;

    public static MetaStreamSections? ReadInnerFile(MetaStreamSections bundle, BundleEntry entry) =>
        Contains(bundle, entry) ? MetaStreamParser.Parse(bundle.Async.AsSpan((int)entry.Offset, (int)entry.Size)) : null;
}
