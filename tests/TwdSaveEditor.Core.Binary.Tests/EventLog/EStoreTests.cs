using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Core.Binary.Tests.EventLog;

public class EStoreTests
{
    [Fact]
    public void S3_EStoreReader_ParsesEventLog()
    {
        var estorePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id.estore");
        var entries = EStoreReader.ReadEventLog(estorePath);
        Assert.True(entries.Count > 0, "Expected EventLog entries from estore/epage");

        var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
        Assert.True(dialogNodes.Count > 0, "Expected dialog node events");

        var beginEps = entries.Where(e => e.EventTypeHash == EventLogEventTypes.BeginEpisode).ToList();
        Assert.True(beginEps.Count > 0, "Expected Begin Episode events");
    }

    [Fact]
    public void EStoreCreator_ProducesCorrectMsv6Header()
    {
        var events = new List<EventLogEntry>
        {
            new()
            {
                EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
                NodeHash = 0x2D4BB68B3A6B79B7,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = 0,
                Trailing = 0,
            }
        };

        var (estore, epage, epageFilename) = EStoreCreator.Create("_wd3_test_id", events);

        Assert.Equal(MetaStreamHeader.MagicMsv6, BitConverter.ToUInt32(estore, 0));
        Assert.Equal(MetaStreamHeader.MagicMsv6, BitConverter.ToUInt32(epage, 0));

        var estoreVerCount = BitConverter.ToUInt32(estore, 16);
        var epageVerCount = BitConverter.ToUInt32(epage, 16);
        Assert.Equal(5u, estoreVerCount);
        Assert.Equal(5u, epageVerCount);
    }

    [Fact]
    public void EStoreCreator_RecordFormatMatches42Bytes()
    {
        var entry = new EventLogEntry
        {
            EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
            NodeHash = 0x2D4BB68B3A6B79B7,
            ValueType = 1,
            ExtraFlag = 0,
            SequenceIndex = 0,
            Trailing = 0,
        };

        var record = EStoreCreator.BuildRecord(entry);

        Assert.Equal(42, record.Length);

        Assert.Equal(0x0Au, BitConverter.ToUInt32(record, 0));
        Assert.Equal(0x22u, BitConverter.ToUInt32(record, 4));
        Assert.Equal(0x01u, BitConverter.ToUInt32(record, 8));
        Assert.Equal(0x00u, BitConverter.ToUInt32(record, 12));

        Assert.Equal(EventLogEventTypes.ExecutingDialogNode, BitConverter.ToUInt64(record, 16));

        Assert.Equal(0x2D4BB68B3A6B79B7UL, BitConverter.ToUInt64(record, 29));
    }

    [Fact]
    public void EStoreCreator_RecordMatchesRealEpageFormat()
    {
        var realEpagePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id_Page734.epage");
        var realData = File.ReadAllBytes(realEpagePath);

        var realEntries = EStoreReader.ReadEPage(realEpagePath);
        Assert.NotEmpty(realEntries);

        var firstReal = realEntries[0];

        var created = EStoreCreator.BuildRecord(firstReal);

        Assert.Equal(42, created.Length);
        Assert.Equal(firstReal.RawData.Length, created.Length);

        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(firstReal.RawData[i], created[i]);
        }

        Assert.Equal(
            BitConverter.ToUInt64(firstReal.RawData, 16),
            BitConverter.ToUInt64(created, 16));

        Assert.Equal(
            BitConverter.ToUInt64(firstReal.RawData, 29),
            BitConverter.ToUInt64(created, 29));
    }

    [Fact]
    public void EStoreCreator_VersionEntriesDocumented()
    {
        var realEstorePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id.estore");
        var realData = File.ReadAllBytes(realEstorePath);

        var realVerCount = BitConverter.ToUInt32(realData, 16);
        Assert.Equal(5u, realVerCount);

        var expectedReal = new (ulong TypeCrc, uint VersionCrc)[]
        {
            (0x3AAEB61240D3CFBA, 0xD8D22CB9),
            (0xBEBB886A0541595F, 0xB59B0682),
            (0x004F023463D89FB0, 0xB539B0FF),
            (0x24032A7AD8BB721D, 0x739CE237),
            (0x238A520C4A924AA6, 0x2E4AF103),
        };

        int pos = 20;
        for (int i = 0; i < realVerCount; i++)
        {
            var tc = BitConverter.ToUInt64(realData, pos);
            pos += 8;
            var vc = BitConverter.ToUInt32(realData, pos);
            pos += 4;
            Assert.Equal(expectedReal[i].TypeCrc, tc);
            Assert.Equal(expectedReal[i].VersionCrc, vc);
        }

        var events = new List<EventLogEntry>
        {
            new()
            {
                EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
                NodeHash = 0x2D4BB68B3A6B79B7,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = 0,
                Trailing = 0,
            }
        };
        var (estore, _, _) = EStoreCreator.Create("_wd3_test", events);

        var createdVerCount = BitConverter.ToUInt32(estore, 16);
        Assert.Equal(5u, createdVerCount);

        int cpos = 20;
        for (int i = 0; i < 5; i++)
        {
            var tc = BitConverter.ToUInt64(estore, cpos); cpos += 8;
            var vc = BitConverter.ToUInt32(estore, cpos); cpos += 4;
            Assert.Equal(expectedReal[i].TypeCrc, tc);
            Assert.Equal(expectedReal[i].VersionCrc, vc);
        }
    }

    [Theory]
    [InlineData("Executing Dialog Node", 0x625874A31EA13BB1UL)]
    [InlineData("Dialog Choice", 0x25D62FD9BE53CF73UL)]
    [InlineData("Begin Episode", 0x22B4F702006E4E3AUL)]
    [InlineData("End Episode", 0xB1BB1124EA852E99UL)]
    [InlineData("Save Serial", 0x48FA4CC44ADE92F3UL)]
    public void EventTypeHashes_MatchExpected(string name, ulong expectedHash)
    {
        var computed = TelltaleHash.ComputeCrc64(name);
        Assert.Equal(expectedHash, computed);
    }
}
