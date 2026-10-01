using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Bundles;

public static class BundleFileTable
{
    private const uint MaxFiles = 20000;

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

            var nameStart = position;
            var nameEnd = position;
            while (position < table.Length)
            {
                if (table[position++] == 0)
                    break;

                nameEnd = position;
            }

            var nameLength = nameEnd - nameStart + 1;
            position += ((nameLength + 3) & ~3) - nameLength;
            if (position + 16 > table.Length)
                break;

            position += 16;
            entries.Add(new BundleEntry(TextFormat.DecodeAscii(table[nameStart..nameEnd]), offset, size));
        }

        return entries;
    }

    public static bool Contains(MetaStreamSections bundle, BundleEntry entry) =>
        (long)entry.Offset + entry.Size <= bundle.Async.Length;

    public static MetaStreamSections? ReadInnerFile(MetaStreamSections bundle, BundleEntry entry) =>
        Contains(bundle, entry) ? MetaStreamParser.Parse(bundle.Async.AsSpan((int)entry.Offset, (int)entry.Size)) : null;
}
