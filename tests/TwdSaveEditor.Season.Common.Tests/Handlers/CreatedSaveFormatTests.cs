using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Season.Common.Extensions;

namespace TwdSaveEditor.Season.Common.Tests.Handlers;

public class CreatedSaveFormatTests
{
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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
        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get(seasonKey)!;
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

        var handler = TestSeasons.Registry.Get("s1")!;
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

        var handler = TestSeasons.Registry.Get("s2")!;
        var createdSlot = handler.CreateBlankSave("wd2_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.Choices);
        Assert.NotNull(createdSlot.Choices);

        Assert.Equal(2u, createdSlot.Choices!.Version);

        Assert.Equal("season1.prop", realSlot.ChoicesFileName);

        Assert.Contains(createdSlot.Choices.TypeGroups,
            g => g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);
    }

    [Theory]
    [InlineData("s1", 5)]
    [InlineData("s2", 5)]
    [InlineData("s3", 5)]
    [InlineData("s4", 4)]
    [InlineData("michonne", 3)]
    public void CreatedSave_WritesAndReadsBackWithCorrectStructure(string seasonKey, int episode)
    {
        var handler = TestSeasons.Registry.Get(seasonKey)!;
        var fileName = $"{handler.FilePrefix}roundtrip.bundle";
        var createdSlot = TestSeasons.Registry.CreateSave(seasonKey, episode, fileName);

        var bundleBytes = BundleWriter.Write(createdSlot);
        Assert.True(bundleBytes.Length > 0);

        var readBack = BundleReader.Read(bundleBytes, fileName);

        Assert.Equal(MetaStreamHeader.MagicMsv6, readBack.OuterHeader.Magic);
        Assert.NotNull(readBack.Metadata);
        Assert.Equal(2u, readBack.Metadata!.Version);
        Assert.Equal(0x100u, readBack.Metadata.Flags);

        Assert.True(readBack.Metadata.AllProperties.Any());

        Assert.Equal(createdSlot.Files.Count, readBack.Files.Count);
        for (int i = 0; i < createdSlot.Files.Count; i++)
        {
            Assert.Equal(createdSlot.Files[i].Name, readBack.Files[i].Name);
            Assert.Equal(createdSlot.Files[i].NameSymbol, readBack.Files[i].NameSymbol);
            Assert.Equal(createdSlot.Files[i].TypeSymbol, readBack.Files[i].TypeSymbol);
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
