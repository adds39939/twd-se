using TwdSaveEditor.Tools.Common.Binary;

namespace TwdSaveEditor.Tools.Common.Props;

public sealed class PropReader(byte[] data)
{
    public long Position { get; set; }

    public long Remaining => data.Length - Position;

    public byte U8()
    {
        var value = data[Position];
        Position += 1;
        return value;
    }

    public uint U32()
    {
        var value = Bytes.U32(data, Position);
        Position += 4;
        return value;
    }

    public int I32()
    {
        var value = Bytes.I32(data, Position);
        Position += 4;
        return value;
    }

    public ulong U64()
    {
        var value = Bytes.U64(data, Position);
        Position += 8;
        return value;
    }

    public ReadOnlySpan<byte> Read(long count)
    {
        var value = Bytes.Slice(data, Position, Position + count);
        Position += count;
        return value;
    }
}
