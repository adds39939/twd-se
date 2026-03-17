using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

public class BinaryReaderWriterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TelltaleBool_RoundTrips(bool value)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
            writer.WriteTelltaleBool(value);

        ms.Position = 0;
        using var reader = new BinaryReaderEx(ms);
        Assert.Equal(value, reader.ReadTelltaleBool());
    }

    [Fact]
    public void TelltaleBool_TrueIs0x31_FalseIs0x30()
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
        {
            writer.WriteTelltaleBool(true);
            writer.WriteTelltaleBool(false);
        }

        Assert.Equal(0x31, ms.ToArray()[0]);
        Assert.Equal(0x30, ms.ToArray()[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("test string with spaces")]
    public void LengthPrefixedString_RoundTrips(string value)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
            writer.WriteLengthPrefixedString(value);

        ms.Position = 0;
        using var reader = new BinaryReaderEx(ms);
        Assert.Equal(value, reader.ReadLengthPrefixedString());
    }

    [Fact]
    public void Symbol_RoundTrips()
    {
        var sym = new Symbol(0xDEADBEEFCAFEBABE);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
            writer.WriteSymbol(sym);

        ms.Position = 0;
        using var reader = new BinaryReaderEx(ms);
        Assert.Equal(sym, reader.ReadSymbol());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Int32_RoundTrips(int value)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
            writer.WriteInt32(value);

        ms.Position = 0;
        using var reader = new BinaryReaderEx(ms);
        Assert.Equal(value, reader.ReadInt32());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(3.14f)]
    [InlineData(-1.5f)]
    public void Float_RoundTrips(float value)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriterEx(ms, leaveOpen: true))
            writer.WriteFloat(value);

        ms.Position = 0;
        using var reader = new BinaryReaderEx(ms);
        Assert.Equal(value, reader.ReadFloat());
    }
}
