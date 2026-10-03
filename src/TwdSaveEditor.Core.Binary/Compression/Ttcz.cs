using System.Buffers.Binary;
using System.IO.Compression;

namespace TwdSaveEditor.Core.Binary.Compression;

public static class Ttcz
{
    public const uint Magic = 0x5454435A;
    public const int PageSize = 0x10000;

    private const int HeaderSize = 12;

    public static bool HasMagic(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic;

    public static byte[] Decompress(ReadOnlySpan<byte> data)
    {
        var pageSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var pageCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);

        var offsets = new long[pageCount + 1];
        for (var i = 0; i <= pageCount; i++)
        {
            offsets[i] = (long)BinaryPrimitives.ReadUInt64LittleEndian(data[(HeaderSize + i * 8)..]);
        }

        using var output = new MemoryStream();
        var page = new byte[pageSize];
        for (var i = 0; i < pageCount; i++)
        {
            var block = data[(int)offsets[i]..(int)offsets[i + 1]];
            using var input = new MemoryStream(block.ToArray());
            using var inflater = new DeflateStream(input, CompressionMode.Decompress);
            var total = 0;
            int read;
            while (total < pageSize && (read = inflater.Read(page, total, pageSize - total)) > 0)
            {
                total += read;
            }

            output.Write(page, 0, total);
        }

        return output.ToArray();
    }

    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var pageCount = Math.Max(1, (data.Length + PageSize - 1) / PageSize);
        var pages = new byte[pageCount][];
        var page = new byte[PageSize];
        for (var i = 0; i < pageCount; i++)
        {
            Array.Clear(page);
            var start = i * PageSize;
            data[start..Math.Min(data.Length, start + PageSize)].CopyTo(page);

            using var output = new MemoryStream();
            using (var deflater = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflater.Write(page);
            }

            pages[i] = output.ToArray();
        }

        var tableSize = HeaderSize + (pageCount + 1) * 8;
        var result = new byte[tableSize + pages.Sum(block => block.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(result, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), PageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)pageCount);

        var offset = tableSize;
        for (var i = 0; i < pageCount; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(HeaderSize + i * 8), (ulong)offset);
            pages[i].CopyTo(result, offset);
            offset += pages[i].Length;
        }

        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(HeaderSize + pageCount * 8), (ulong)offset);
        return result;
    }
}
