using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.ValidateSaves.Archives;

public static class ArchiveDirectoryParser
{
    private const uint MaxFiles = 100000;
    private const uint MaxNameLength = 500;

    private static readonly int[] AlternativeOffsets = [4, 8, 12];

    public static byte[]? Find(ReadOnlySpan<byte> data, string target)
    {
        if (data.Length < 4)
        {
            return null;
        }

        var fileCount = Bytes.U32(data, 0);
        if (fileCount is > MaxFiles or 0)
        {
            return null;
        }

        long position = 4;
        var entries = new List<(string Name, ulong Offset, uint Size)>();
        for (uint i = 0; i < fileCount; i++)
        {
            if (position + 4 > data.Length)
            {
                break;
            }

            var nameLength = Bytes.U32(data, position);
            position += 4;
            if (nameLength > MaxNameLength || position + nameLength > data.Length)
            {
                return FindAlternative(data, target);
            }

            var name = TextFormat.DecodeAscii(data.Slice((int)position, (int)nameLength));
            position += (nameLength + 7) & ~7u;

            if (position + 12 > data.Length)
            {
                break;
            }

            entries.Add((name, Bytes.U64(data, position), Bytes.U32(data, position + 8)));
            position += 12;
        }

        var matching = entries.Where(entry => string.Equals(entry.Name, target, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var (_, offset, size) in matching)
        {
            if ((ulong)position + offset + size <= (ulong)data.Length)
            {
                return data.Slice((int)((ulong)position + offset), (int)size).ToArray();
            }
        }

        foreach (var (_, offset, size) in matching)
        {
            if (offset + size <= (ulong)data.Length)
            {
                return data.Slice((int)offset, (int)size).ToArray();
            }
        }

        return null;
    }

    private static byte[]? FindAlternative(ReadOnlySpan<byte> data, string target)
    {
        foreach (var skip in AlternativeOffsets)
        {
            var result = Find(Bytes.Slice(data, skip, data.Length), target);
            if (result is { Length: > 0 })
            {
                return result;
            }
        }

        return null;
    }
}
