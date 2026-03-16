using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Extended binary reader with Telltale-specific read helpers.
/// All reads are little-endian (default for BinaryReader on LE platforms).
/// </summary>
public sealed class BinaryReaderEx : IDisposable
{
    private readonly BinaryReader _reader;

    public BinaryReaderEx(Stream stream, bool leaveOpen = false)
    {
        _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen);
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

    /// <summary>
    /// Telltale bools: 0x31 ('1') = true, 0x30 ('0') = false.
    /// </summary>
    public bool ReadTelltaleBool()
    {
        var b = _reader.ReadByte();
        return b == 0x31;
    }

    /// <summary>
    /// Length-prefixed ASCII string: u32 length + chars (no null terminator stored).
    /// </summary>
    public string ReadLengthPrefixedString()
    {
        var length = _reader.ReadInt32();
        if (length <= 0)
            return string.Empty;
        var bytes = _reader.ReadBytes(length);
        return Encoding.ASCII.GetString(bytes);
    }

    public Symbol ReadSymbol() => new(_reader.ReadUInt64());

    public void Dispose() => _reader.Dispose();
}
