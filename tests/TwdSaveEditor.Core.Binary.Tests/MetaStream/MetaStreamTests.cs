using TwdSaveEditor.Core.Binary.Compression;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.MetaStream;

public class MetaStreamTests
{
    [Fact]
    public void MetaStream_UncompressedRoundTrip()
    {
        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv5,
            VersionEntries =
            [
                new VersionEntry(0xDEADBEEF, 1)
            ]
        };
        var content = new MetaStreamContent(header, [1, 2, 3, 4, 5, 6, 7, 8], [9, 9], [7, 7, 7]);

        var read = MetaStreamCodec.Read(MetaStreamCodec.Write(content));

        Assert.Equal(MetaStreamHeader.MagicMsv5, read.Header.Magic);
        Assert.Equal(content.Default, read.Default);
        Assert.Equal(content.Debug, read.Debug);
        Assert.Equal(content.Async, read.Async);
        Assert.False(read.Header.IsDefaultCompressed);
        Assert.Single(read.Header.VersionEntries);
        Assert.Equal(0xDEADBEEFUL, read.Header.VersionEntries[0].TypeCrc);
        Assert.Equal(1U, read.Header.VersionEntries[0].VersionCrc);
    }

    [Fact]
    public void MetaStream_CompressedSectionsKeepTheirFlagsAndContent()
    {
        var data = new byte[Ttcz.PageSize + 300];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 10);

        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            DefaultSectionSize = MetaStreamHeader.CompressedFlag,
            AsyncSectionSize = MetaStreamHeader.CompressedFlag,
        };

        var read = MetaStreamCodec.Read(MetaStreamCodec.Write(new MetaStreamContent(header, data, [1, 2], data)));

        Assert.True(read.Header.IsDefaultCompressed);
        Assert.False(read.Header.IsDebugCompressed);
        Assert.True(read.Header.IsAsyncCompressed);
        Assert.Equal(2 * Ttcz.PageSize, read.Default.Length);
        Assert.Equal(data, read.Default[..data.Length]);
        Assert.All(read.Default[data.Length..], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 1, 2 }, read.Debug);
    }

    [Fact]
    public void Ttcz_CompressedDataStartsWithPageTable()
    {
        var compressed = Ttcz.Compress(new byte[10]);

        Assert.True(Ttcz.HasMagic(compressed));
        Assert.Equal((uint)Ttcz.PageSize, BitConverter.ToUInt32(compressed, 4));
        Assert.Equal(1U, BitConverter.ToUInt32(compressed, 8));
        Assert.Equal(28UL, BitConverter.ToUInt64(compressed, 12));
        Assert.Equal((ulong)compressed.Length, BitConverter.ToUInt64(compressed, 20));
        Assert.Equal(Ttcz.PageSize, Ttcz.Decompress(compressed).Length);
    }

    [Fact]
    public void MetaStream_InvalidMagic_Throws()
    {
        var data = new byte[20];
        BitConverter.GetBytes(0x12345678U).CopyTo(data, 0);

        Assert.Throws<InvalidDataException>(() => MetaStreamCodec.Read(data));
    }
}
