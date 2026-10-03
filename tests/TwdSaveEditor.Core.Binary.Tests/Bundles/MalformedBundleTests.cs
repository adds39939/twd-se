using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Core.Binary.Tests.Bundles;

public class MalformedBundleTests
{
    [Fact]
    public void BundleWithATruncatedFileTable_IsRejectedInsteadOfLoadingEmpty()
    {
        var file = MetaStreamCodec.Write(new MetaStreamContent(new MetaStreamHeader { Magic = MetaStreamHeader.MagicMsv6 }, [1, 0, 0, 0], [], new byte[100]));

        Assert.Throws<InvalidDataException>(() => BundleReader.Read(file, "wd3_saveslot1.bundle"));
    }

    [Fact]
    public void PropertyFileThatCannotBeDecompressed_IsReportedAsUnreadable()
    {
        var file = BundleFileEntry.Create(BundleFileNames.SlotMetadata, TelltaleTypes.PropertySet, MalformedFiles.CompressedWithOversizedPage());

        Assert.False(BundleReader.TryParseProperties(file));
        Assert.Null(file.Properties);
    }
}
