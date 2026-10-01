using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Primitives;

public sealed class BinaryWriterEx : IDisposable
{
    private readonly BinaryWriter _writer;

    public BinaryWriterEx(Stream stream, bool leaveOpen = false)
    {
        _writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen);
    }

    public long Position => _writer.BaseStream.Position;

    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteBytes(byte[] data) => _writer.Write(data);
    public void WriteUInt32(uint value) => _writer.Write(value);
    public void WriteInt32(int value) => _writer.Write(value);
    public void WriteUInt64(ulong value) => _writer.Write(value);
    public void WriteFloat(float value) => _writer.Write(value);

    public void WriteTelltaleBool(bool value) => _writer.Write((byte)(value ? 0x31 : 0x30));

    public void WriteLengthPrefixedString(string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value ?? string.Empty);
        _writer.Write(bytes.Length);
        if (bytes.Length > 0)
            _writer.Write(bytes);
    }

    public void WriteSymbol(Symbol symbol) => _writer.Write(symbol.Value);

    public void Flush() => _writer.Flush();
    public void Dispose() => _writer.Dispose();
}
