using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Archives;

public static class InnerArchive
{
    public const uint Magic = 0x54544134;

    private const int HeaderSize = 12;
    private const int EntrySize = 28;
    private const uint NamePageSize = 0x10000;

    public static OrderedDictionary<string, ReadOnlyMemory<byte>> Parse(ReadOnlyMemory<byte> archive)
    {
        var files = new OrderedDictionary<string, ReadOnlyMemory<byte>>();
        if (archive.Length < HeaderSize)
        {
            return files;
        }

        if (Bytes.U32(archive.Span, 0) != Magic)
        {
            var position = Bytes.IndexOf(archive.Span, "4ATT"u8);
            if (position < 0)
            {
                return files;
            }

            archive = archive[position..];
        }

        var data = archive.Span;
        var namesSize = Bytes.U32(data, 4);
        var fileCount = Bytes.U32(data, 8);

        var entries = new List<(uint NameOffset, uint FileOffset, uint FileSize)>();
        for (long i = 0; i < fileCount; i++)
        {
            var entry = HeaderSize + i * EntrySize;
            if (entry + EntrySize > data.Length)
            {
                break;
            }

            var nameOffset = (uint)Bytes.U16(data, entry + 24) * NamePageSize + Bytes.U16(data, entry + 26);
            entries.Add((nameOffset, Bytes.U32(data, entry + 8), Bytes.U32(data, entry + 16)));
        }

        var nameTableStart = HeaderSize + (long)fileCount * EntrySize;
        var fileDataStart = nameTableStart + namesSize;

        foreach (var (nameOffset, fileOffset, fileSize) in entries)
        {
            var nameStart = nameTableStart + nameOffset;
            long nameEnd = Bytes.IndexOf(data, [0], nameStart);
            if (nameEnd < 0)
            {
                nameEnd = nameTableStart + namesSize;
            }

            var name = TextFormat.DecodeAscii(Bytes.Slice(data, nameStart, nameEnd));
            var start = fileDataStart + fileOffset;
            if (start + fileSize <= data.Length)
            {
                files[name] = archive.Slice((int)start, (int)fileSize);
            }
        }

        return files;
    }
}
