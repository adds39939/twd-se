using System.Buffers.Binary;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.PropertySets;

public class MalformedPropertySetTests
{
    private const int HeaderSize = 12;
    private const int NestingSize = 28;
    private const int EmptyBodySize = 8;

    private readonly PropertySetReader _reader = new();

    private static byte[] Nested(int depth)
    {
        var data = new byte[(depth - 1) * (HeaderSize + NestingSize) + HeaderSize + EmptyBodySize];
        var position = 0;
        for (var level = 1; level <= depth; level++)
        {
            var body = (depth - level) * (HeaderSize + NestingSize) + EmptyBodySize;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(position), new PropertySet().Version);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(position + 8), (uint)(sizeof(uint) + body));
            position += HeaderSize;
            if (level == depth)
            {
                break;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(position + 4), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(position + 8), TelltaleTypes.PropertySet);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(position + 16), 1);
            position += NestingSize;
        }

        return data;
    }

    [Fact]
    public void NestedTestData_MatchesWhatTheWriterProduces()
    {
        var outer = new PropertySet();
        outer.Set(new Symbol(0), new Symbol(TelltaleTypes.PropertySet), new PropertySetValue(new PropertySet()));

        Assert.Equal(new PropertySetWriter().Write(new PropertySet()), Nested(1));
        Assert.Equal(new PropertySetWriter().Write(outer), Nested(2));
    }

    [Fact]
    public void DeeplyNestedPropertySets_AreRejectedWithoutOverflowingTheStack()
    {
        Assert.Throws<InvalidDataException>(() => _reader.Read(Nested(20_000)));
    }

    [Fact]
    public void NestedPropertySets_AreReadUpToTheDepthLimit()
    {
        Assert.NotNull(_reader.Read(Nested(PropertySetReader.MaxDepth)));
        Assert.Throws<InvalidDataException>(() => _reader.Read(Nested(PropertySetReader.MaxDepth + 1)));
    }

    [Fact]
    public void StringLengthBeyondTheData_IsRejected()
    {
        var properties = new PropertySet();
        properties.SetString("Name", "value");
        var data = new PropertySetWriter().Write(properties);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(data.Length - "value".Length - sizeof(int)), 0x7FFFFF00);

        Assert.Throws<InvalidDataException>(() => _reader.Read(data));
    }

    [Theory]
    [InlineData("Color", "0000803F0000003F0000803E0000803F")]
    [InlineData("Vector3", "0000803F0000004000004040")]
    [InlineData("AnimOrChore", "0C00000011111111111111110C0000002222222222222222")]
    [InlineData("LocationInfo", "1D0000001500000075695F616374696F6E496E576F726C645F646F776EE00F030905DF0A54200000000000000000000000000000000000803F000000000000000000000000")]
    [InlineData("Map<Symbol,Symbol,less<Symbol>>", "01000000111B4B60A17A75FC2312FF65DFBE1515")]
    [InlineData("SoundEventName<0>", "14000000EB21F77B933EB86B0000000000000000")]
    [InlineData("ScriptEnum:AIDummyPos", "0D000000050000004C65667431")]
    public void ValuesOfFixedLayoutTypes_AreReadWhole(string type, string value)
    {
        var bytes = Convert.FromHexString(value);
        var properties = new PropertySet();
        properties.Set(Symbol.FromString("Value"), Symbol.FromString(type), new RawBytesValue(bytes, Symbol.FromString(type)));
        properties.SetBool("After", true);
        var data = new PropertySetWriter().Write(properties);

        var read = _reader.Read(data);

        Assert.Equal(bytes, Assert.IsType<RawBytesValue>(read.Find("Value")!.Value).Data);
        Assert.True(read.GetBool("After"));
        Assert.Equal(data, new PropertySetWriter().Write(read));
    }

    [Fact]
    public void HandleValues_AreReadAsSymbols()
    {
        var properties = new PropertySet();
        properties.Set(Symbol.FromString("Dialog File"), new Symbol(TelltaleTypes.DialogHandle), new SymbolValue(Symbol.FromString("env_junkyard.dlog")));

        var read = _reader.Read(new PropertySetWriter().Write(properties));

        Assert.Equal(Symbol.FromString("env_junkyard.dlog"), Assert.IsType<SymbolValue>(read.Find("Dialog File")!.Value).Value);
    }

    [Fact]
    public void ValuesOfUnknownTypes_AreRejected()
    {
        var properties = new PropertySet();
        properties.Set(Symbol.FromString("Value"), Symbol.FromString("NotATelltaleType"), new RawBytesValue([4, 0, 0, 0, 1, 2, 3, 4], Symbol.FromString("NotATelltaleType")));

        var error = Assert.Throws<InvalidDataException>(() => _reader.Read(new PropertySetWriter().Write(properties)));

        Assert.Contains("Unknown property type", error.Message);
    }
}
