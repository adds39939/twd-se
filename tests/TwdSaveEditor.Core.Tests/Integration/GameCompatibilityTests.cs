using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Core.Tests.Integration;

public class GameCompatibilityTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(),
        new S2Handler(),
        new S3Handler(),
        new S4Handler(),
        new MichonneHandler(),
    ]);

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasSameMagicAsRealSave(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.Equal(realSlot.OuterHeader.Magic, createdSlot.OuterHeader.Magic);
        Assert.Equal(MetaStreamHeader.MagicMsv6, createdSlot.OuterHeader.Magic);
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasSameOuterVersionEntries(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.Equal(realSlot.OuterHeader.VersionEntries.Count, createdSlot.OuterHeader.VersionEntries.Count);

        for (int i = 0; i < realSlot.OuterHeader.VersionEntries.Count; i++)
        {
            var real = realSlot.OuterHeader.VersionEntries[i];
            var created = createdSlot.OuterHeader.VersionEntries[i];
            Assert.Equal(real.TypeCrc, created.TypeCrc);
            Assert.Equal(real.VersionCrc, created.VersionCrc);
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasCorrectInnerFileNames(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.Contains(createdSlot.Files, f => f.Name == "metadata_slot.p");

        Assert.Contains(realSlot.Files, f => f.Name == "metadata_slot.p");

        if (seasonKey == "s1")
        {
            Assert.Contains(createdSlot.Files, f => f.Name == "choices.prop");
        }
        else if (seasonKey == "s2")
        {
            Assert.Contains(createdSlot.Files, f => f.Name == "season1.prop");
        }
        else if (seasonKey == "s4")
        {
            Assert.Contains(createdSlot.Files, f => f.Name == "choicestats.pro");
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasMatchingFileTableHashes(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        foreach (var createdEntry in createdSlot.Files)
        {
            var realEntry = realSlot.Files.FirstOrDefault(f => f.Name == createdEntry.Name);
            if (realEntry != null)
            {
                Assert.Equal(realEntry.NameSymbol, createdEntry.NameSymbol);
                Assert.Equal(realEntry.TypeSymbol, createdEntry.TypeSymbol);
            }
        }
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s2")]
    [InlineData("s3")]
    [InlineData("s4")]
    [InlineData("michonne")]
    public void CreatedSave_InnerMetaStreamHasMsv6Magic(string seasonKey)
    {
        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        var written = BundleReader.Read(BundleWriter.Write(createdSlot), createdSlot.FileName);

        foreach (var file in written.Files)
        {
            Assert.True(file.Data.Length >= 4, $"Inner file {file.Name} too small");
            var magic = BitConverter.ToUInt32(file.Data, 0);
            Assert.Equal(MetaStreamHeader.MagicMsv6, magic);
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_InnerVersionEntriesMatchReal(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        var written = BundleReader.Read(BundleWriter.Write(createdSlot), createdSlot.FileName);

        var realMetadata = realSlot.FindFile(BundleFileNames.SlotMetadata)!.Data;
        var createdMetadata = written.FindFile(BundleFileNames.SlotMetadata)!.Data;

        var realInnerVers = ParseInnerVersionEntries(realMetadata);
        var createdInnerVers = ParseInnerVersionEntries(createdMetadata);

        Assert.Equal(realInnerVers.Count, createdInnerVers.Count);
        for (int i = 0; i < realInnerVers.Count; i++)
        {
            Assert.Equal(realInnerVers[i].TypeCrc, createdInnerVers[i].TypeCrc);
            Assert.Equal(realInnerVers[i].VersionCrc, createdInnerVers[i].VersionCrc);
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_MetadataPropertySetMatchesRealFormat(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.Metadata);
        Assert.NotNull(createdSlot.Metadata);

        Assert.Equal(realSlot.Metadata!.Version, createdSlot.Metadata!.Version);
        Assert.Equal(realSlot.Metadata.Flags, createdSlot.Metadata.Flags);

        Assert.Equal(2u, createdSlot.Metadata.Version);
        Assert.Equal(0x100u, createdSlot.Metadata.Flags);
    }

    [Fact]
    public void CreatedSave_S1ChoicesPropertySetHasCorrectStructure()
    {
        var realPath = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s1")!;
        var createdSlot = handler.CreateBlankSave("wd1_test.bundle", handler.GetEpisodeId(2));

        Assert.NotNull(realSlot.Choices);
        Assert.NotNull(createdSlot.Choices);

        Assert.Equal(realSlot.Choices!.Version, createdSlot.Choices!.Version);
        Assert.Equal(2u, createdSlot.Choices.Version);

        Assert.Equal(0x0u, createdSlot.Choices.Flags);

        Assert.Contains(createdSlot.Choices.TypeGroups,
            g => g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);
    }

    [Fact]
    public void CreatedSave_S2ChoicesPropertySetHasCorrectStructure()
    {
        var realPath = TestDataHelper.GetPath("S2", "wd2_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s2")!;
        var createdSlot = handler.CreateBlankSave("wd2_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.Choices);
        Assert.NotNull(createdSlot.Choices);

        Assert.Equal(2u, createdSlot.Choices!.Version);

        Assert.Equal("season1.prop", realSlot.ChoicesFileName);

        Assert.Contains(createdSlot.Choices.TypeGroups,
            g => g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);
    }

    [Fact]
    public void CreatedSave_S4ChoiceStatsPropertySetMatchesRealFormat()
    {
        var realPath = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s4")!;
        var createdSlot = handler.CreateBlankSave("wd4_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.ChoiceStats);
        Assert.NotNull(createdSlot.ChoiceStats);

        Assert.Equal(realSlot.ChoiceStats!.Version, createdSlot.ChoiceStats!.Version);
        Assert.Equal(realSlot.ChoiceStats.Flags, createdSlot.ChoiceStats.Flags);

        Assert.Equal(2u, createdSlot.ChoiceStats.Version);
        Assert.Equal(0x100u, createdSlot.ChoiceStats.Flags);
    }

    [Theory]
    [InlineData("s1", 5)]
    [InlineData("s2", 5)]
    [InlineData("s3", 5)]
    [InlineData("s4", 4)]
    [InlineData("michonne", 3)]
    public void CreatedSave_WritesAndReadsBackWithCorrectStructure(string seasonKey, int episode)
    {
        var handler = Registry.Get(seasonKey)!;
        var fileName = $"{handler.FilePrefix}roundtrip.bundle";
        var createdSlot = Registry.CreateSave(seasonKey, episode, fileName);

        var bundleBytes = BundleWriter.Write(createdSlot);
        Assert.True(bundleBytes.Length > 0);

        var readBack = BundleReader.Read(bundleBytes, fileName);

        Assert.Equal(MetaStreamHeader.MagicMsv6, readBack.OuterHeader.Magic);
        Assert.NotNull(readBack.Metadata);
        Assert.Equal(2u, readBack.Metadata!.Version);
        Assert.Equal(0x100u, readBack.Metadata.Flags);

        var accessor = new SaveAccessor(readBack.Choices, readBack.Metadata);
        var episodeId = accessor.GetMetadataString("episodeId");
        Assert.True(readBack.Metadata.AllProperties.Any());

        Assert.Equal(createdSlot.Files.Count, readBack.Files.Count);
        for (int i = 0; i < createdSlot.Files.Count; i++)
        {
            Assert.Equal(createdSlot.Files[i].Name, readBack.Files[i].Name);
            Assert.Equal(createdSlot.Files[i].NameSymbol, readBack.Files[i].NameSymbol);
            Assert.Equal(createdSlot.Files[i].TypeSymbol, readBack.Files[i].TypeSymbol);
        }
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

    [Fact]
    public void S4ChoiceStats_GuidFormatMatchesRealSave()
    {
        var realPath = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        Assert.NotNull(realSlot.ChoiceStats);

        var rawProp = realSlot.ChoiceStats!.AllProperties.FirstOrDefault();
        Assert.NotNull(rawProp);
        Assert.IsType<StringValue>(rawProp!.Value);

        var rawString = ((StringValue)rawProp.Value).Value;

        if (!string.IsNullOrEmpty(rawString))
        {
            var entries = rawString.Split('\t');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                Assert.Matches(@"\(\s*\{[0-9A-Fa-f-]+\}\s*\)", entry);
            }
        }
    }

    private static List<VersionEntry> ParseInnerVersionEntries(byte[] innerData)
    {
        var entries = new List<VersionEntry>();
        if (innerData.Length < 20) return entries;

        var verCount = BitConverter.ToUInt32(innerData, 16);
        int pos = 20;
        for (int i = 0; i < verCount; i++)
        {
            if (pos + 12 > innerData.Length) break;
            var typeCrc = BitConverter.ToUInt64(innerData, pos);
            pos += 8;
            var versionCrc = BitConverter.ToUInt32(innerData, pos);
            pos += 4;
            entries.Add(new VersionEntry(typeCrc, versionCrc));
        }
        return entries;
    }
}
