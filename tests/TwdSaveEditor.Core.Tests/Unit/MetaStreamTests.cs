using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

public class MetaStreamTests
{
    [Fact]
    public void MetaStream_UncompressedRoundTrip()
    {
        var originalData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv5,
            DefaultSectionSize = (uint)originalData.Length,
            DebugSectionSize = 0,
            AsyncSectionSize = 0,
            VersionEntries =
            [
                new VersionEntry(0xDEADBEEF, 1)
            ]
        };

        var fileBytes = MetaStreamWriter.Write(header, originalData);

        using var ms = new MemoryStream(fileBytes);
        var (readHeader, readData) = MetaStreamReader.Read(ms);

        Assert.Equal(MetaStreamHeader.MagicMsv5, readHeader.Magic);
        Assert.Equal(originalData, readData);
        Assert.Single(readHeader.VersionEntries);
        Assert.Equal(0xDEADBEEFUL, readHeader.VersionEntries[0].TypeCrc);
        Assert.Equal(1U, readHeader.VersionEntries[0].VersionCrc);
    }

    [Fact]
    public void MetaStream_CompressedRoundTrip()
    {
        // Create some data that's compressible
        var originalData = new byte[256];
        for (int i = 0; i < originalData.Length; i++)
            originalData[i] = (byte)(i % 10);

        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            DefaultSectionSize = 0x80000000 | (uint)originalData.Length, // compressed flag
            DebugSectionSize = 0,
            AsyncSectionSize = 0
        };

        var fileBytes = MetaStreamWriter.Write(header, originalData);

        using var ms = new MemoryStream(fileBytes);
        var (readHeader, readData) = MetaStreamReader.Read(ms);

        Assert.Equal(MetaStreamHeader.MagicMsv6, readHeader.Magic);
        Assert.Equal(originalData, readData);
        Assert.True(readHeader.IsDefaultCompressed);
    }

    [Fact]
    public void MetaStream_InvalidMagic_Throws()
    {
        var data = new byte[20];
        BitConverter.GetBytes(0x12345678U).CopyTo(data, 0);

        using var ms = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => MetaStreamReader.Read(ms));
    }
}
