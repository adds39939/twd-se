using System.Buffers.Binary;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.EventLog;

public class MalformedEventLogTests
{
    private const int NameLengthOffset = 24;

    [Fact]
    public void StorageNameLongerThanTheFile_IsRejected()
    {
        var content = MetaStreamCodec.Read(EventLogCodec.WriteStorage(new EventLogStorage { Name = "_wd3_saveslot1_id.estore" }));
        BinaryPrimitives.WriteInt32LittleEndian(content.Default.AsSpan(NameLengthOffset), 0x7FFFFF00);

        Assert.Throws<InvalidDataException>(() => EventLogCodec.ReadStorage(MetaStreamCodec.Write(content)));
    }

    [Fact]
    public void TruncatedPage_IsRejected()
    {
        var page = new EventLogPage { FlushedName = "_wd3_saveslot1_id_Page734.epage" };
        page.Events.Add(EventLogEvent.ForNumber(1, 0x1234, 1));
        var content = MetaStreamCodec.Read(EventLogCodec.WritePage(page));

        Assert.Throws<InvalidDataException>(() => EventLogCodec.ReadPage(MetaStreamCodec.Write(content with { Default = content.Default[..^4] })));
    }
}
