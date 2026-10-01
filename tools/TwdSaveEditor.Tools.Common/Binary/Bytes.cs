using System.Buffers.Binary;

namespace TwdSaveEditor.Tools.Common.Binary;

public static class Bytes
{
    public static ushort U16(ReadOnlySpan<byte> data, long offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data[checked((int)offset)..]);

    public static uint U32(ReadOnlySpan<byte> data, long offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[checked((int)offset)..]);

    public static int I32(ReadOnlySpan<byte> data, long offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data[checked((int)offset)..]);

    public static ulong U64(ReadOnlySpan<byte> data, long offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data[checked((int)offset)..]);

    public static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, long start, long end)
    {
        var from = (int)Math.Clamp(start, 0, data.Length);
        var to = (int)Math.Clamp(end, from, data.Length);
        return data[from..to];
    }

    public static int IndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern, long start = 0)
    {
        if (start > data.Length)
            return -1;

        var from = (int)Math.Max(start, 0);
        var index = data[from..].IndexOf(pattern);
        return index < 0 ? -1 : from + index;
    }

    public static int Count(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
    {
        var count = 0;
        var position = IndexOf(data, pattern);
        while (position >= 0)
        {
            count++;
            position = IndexOf(data, pattern, position + pattern.Length);
        }

        return count;
    }

    public static List<int> FindAll(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern, long start = 0)
    {
        var positions = new List<int>();
        var position = IndexOf(data, pattern, start);
        while (position >= 0)
        {
            positions.Add(position);
            position = IndexOf(data, pattern, position + 1);
        }

        return positions;
    }

    public static string Hex(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(data);

    public static byte[] FromU64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }
}
