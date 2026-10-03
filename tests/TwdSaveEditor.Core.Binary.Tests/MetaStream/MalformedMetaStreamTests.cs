using System.IO.Compression;
using TwdSaveEditor.Core.Binary.Compression;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Core.Binary.Tests.MetaStream;

public class MalformedMetaStreamTests
{
    [Fact]
    public void SectionLargerThanTheFile_IsRejected()
    {
        var file = MalformedFiles.MetaStream([1, 2, 3, 4]);
        BitConverter.GetBytes(0x7FFFFFFF).CopyTo(file, 4);

        Assert.Throws<InvalidDataException>(() => MetaStreamCodec.Read(file));
    }

    [Theory]
    [InlineData(0x80000000u, 0u)]
    [InlineData(0x10000u, 50_000_000u)]
    [InlineData(0u, 1u)]
    public void TtczHeaderBeyondItsData_IsRejected(uint pageSize, uint pageCount)
    {
        var file = MalformedFiles.MetaStream(MalformedFiles.Ttcz(pageSize, pageCount), compressed: true);

        Assert.Throws<InvalidDataException>(() => MetaStreamCodec.Read(file));
    }

    [Fact]
    public void SectionInflatingBeyondTheLimit_IsRejected()
    {
        using var compressed = new MemoryStream();
        using (var deflater = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflater.Write(new byte[Ttcz.MaxDecompressedSize + 1]);
        }

        var file = MalformedFiles.MetaStream(compressed.ToArray(), compressed: true);

        Assert.Throws<InvalidDataException>(() => MetaStreamCodec.Read(file));
    }
}
