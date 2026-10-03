using System.Buffers.Binary;
using System.IO.Compression;

namespace TwdSaveEditor.Core.Binary.Compression;

public static class Ttcz
{
    public const uint Magic = 0x5454435A;
    public const int PageSize = 0x10000;
    public const int MaxDecompressedSize = 64 * 1024 * 1024;

    private const int HeaderSize = 12;

    public static bool HasMagic(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic;

    public static byte[] Decompress(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new InvalidDataException("TTCZ header is truncated.");
        }

        var pageSize = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var pageCount = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var tableEnd = HeaderSize + ((long)pageCount + 1) * 8;
        if (pageSize is 0 or > MaxDecompressedSize || tableEnd > data.Length || (long)pageCount * pageSize > MaxDecompressedSize)
        {
            throw new InvalidDataException($"TTCZ header is invalid ({pageCount} pages of {pageSize} bytes).");
        }

        var offsets = new long[pageCount + 1];
        for (var i = 0; i <= pageCount; i++)
        {
            offsets[i] = (long)BinaryPrimitives.ReadUInt64LittleEndian(data[(HeaderSize + i * 8)..]);
            if (offsets[i] < tableEnd || offsets[i] > data.Length || (i > 0 && offsets[i] < offsets[i - 1]))
            {
                throw new InvalidDataException($"TTCZ page {i} lies outside the data.");
            }
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
            while (total < page.Length && (read = inflater.Read(page, total, page.Length - total)) > 0)
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
