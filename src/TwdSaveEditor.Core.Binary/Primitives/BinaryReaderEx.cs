using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Primitives;

public sealed class BinaryReaderEx : IDisposable
{
    private readonly BinaryReader _reader;

    public BinaryReaderEx(Stream stream, bool leaveOpen = false)
    {
        _reader = new BinaryReader(stream, Encoding.Latin1, leaveOpen);
    }

    public long Position => _reader.BaseStream.Position;
    public long Length => _reader.BaseStream.Length;
    public long Remaining => Length - Position;

    public byte ReadByte() => _reader.ReadByte();
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);
    public uint ReadUInt32() => _reader.ReadUInt32();
    public int ReadInt32() => _reader.ReadInt32();
    public ulong ReadUInt64() => _reader.ReadUInt64();
    public float ReadFloat() => _reader.ReadSingle();

    public bool ReadTelltaleBool()
    {
        var b = _reader.ReadByte();
        return b == 0x31;
    }

    public string ReadLengthPrefixedString()
    {
        var length = _reader.ReadInt32();
        if (length <= 0)
            return string.Empty;
        var bytes = _reader.ReadBytes(length);
        return Encoding.Latin1.GetString(bytes);
    }

    public Symbol ReadSymbol() => new(_reader.ReadUInt64());

    public void Dispose() => _reader.Dispose();
}
