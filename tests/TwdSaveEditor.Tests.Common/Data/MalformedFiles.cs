using System.Buffers.Binary;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Tests.Common.Data;

public static class MalformedFiles
{
    private const uint TtczMagic = 0x5454435A;

    public static byte[] Truncated(byte[] file) => file[..(file.Length / 2)];

    public static byte[] MetaStream(byte[] defaultSection, bool compressed = false)
    {
        var file = new byte[20 + defaultSection.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(file, MetaStreamHeader.MagicMsv6);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)defaultSection.Length | (compressed ? MetaStreamHeader.CompressedFlag : 0));
        defaultSection.CopyTo(file, 20);
        return file;
    }

    public static byte[] Ttcz(uint pageSize, uint pageCount)
    {
        var data = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(data, TtczMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), pageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), pageCount);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(12), (ulong)data.Length);
        return data;
    }

    public static byte[] CompressedWithOversizedPage() => MetaStream(Ttcz(0x80000000, 0), compressed: true);
}
