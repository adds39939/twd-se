using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Core.Binary.Tests.Bundles;

public class BundleReaderTests
{
    [Fact]
    public void BundleReader_ParsesRealSaveFile()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(path);

        Assert.NotNull(slot);
        Assert.NotEmpty(slot.Files);
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.Choices);
        Assert.True(slot.Choices.TypeGroups.Count > 0, "Choices should have type groups");
    }

    [Fact]
    public void BundleWriter_RoundTripsWithoutCrash()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(path);

        var output = BundleWriter.Write(slot);
        Assert.NotNull(output);
        Assert.True(output.Length > 0);

        var reparsed = BundleReader.Read(output, "test.bundle");
        Assert.NotNull(reparsed.Metadata);
        Assert.NotNull(reparsed.Choices);
        Assert.Equal(slot.Choices!.AllProperties.Count(), reparsed.Choices.AllProperties.Count());
    }

    [Fact]
    public void LoadRealSave_HasReadableMetadata()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(File.ReadAllBytes(path), "wd1_saveslot2.bundle");

        Assert.NotNull(slot.Metadata);

        Assert.Equal(80, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
        Assert.Equal(4, slot.Metadata.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal("WalkingDead104", slot.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal("_wd1_saveslot2_autosave.bundle", slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.True(slot.Metadata.GetBool(SlotMetadataKeys.CompletedEpisode(3)));
        Assert.False(slot.Metadata.GetBool(SlotMetadataKeys.CompletedEpisode(4)));
    }

    [Fact]
    public void AutosaveBundle_ParsesWithMetadata()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        var slot = BundleReader.Read(path);

        Assert.NotNull(slot.Metadata);
        Assert.Equal(2u, slot.Metadata.Version);
        Assert.True(slot.Metadata.AllProperties.Any());
    }

    [Fact]
    public void AutosaveBundle_HandlesHashOnlyFileEntries()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        var slot = BundleReader.Read(path);

        Assert.Equal(423, slot.Files.Count);
        Assert.Equal("metadata_save.p", slot.Files[0].Name);
        Assert.Equal("default.save", slot.Files[1].Name);
        Assert.NotNull(slot.FindFile(BundleFileNames.SaveMetadata));
        Assert.NotNull(slot.FindFile(BundleFileNames.SaveGame));

        var runtimeProperties = slot.Files.Skip(2).ToList();
        Assert.All(runtimeProperties, f => Assert.Equal(string.Empty, f.Name));
        Assert.All(runtimeProperties, f => Assert.Equal(TelltaleTypes.PropertySet, f.TypeSymbol));
        Assert.Equal(runtimeProperties.Count, runtimeProperties.Select(f => f.NameSymbol).Distinct().Count());
    }

    [Fact]
    public void AutosaveBundle_RoundTrips()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        var slot = BundleReader.Read(path);

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "_wd1_saveslot1_autosave.bundle");

        Assert.NotNull(reloaded.Metadata);
        Assert.Equal(slot.Files.Count, reloaded.Files.Count);
    }

    [Theory]
    [InlineData("S1", "wd1_saveslot2.bundle")]
    [InlineData("S2", "wd2_saveslot1.bundle")]
    [InlineData("S3", "wd3_saveslot1.bundle")]
    [InlineData("S4", "wd4_saveslot1.bundle")]
    [InlineData("Michonne", "wdm_saveslot4.bundle")]
    public void LoadRealSave_ParsesCorrectly(string season, string fileName)
    {
        var path = TestDataHelper.GetPath(season, fileName);
        var slot = BundleReader.Read(path);
        slot.DetectedSeasonKey = TestSeasons.Registry.DetectFromFileName(fileName)?.SeasonKey;

        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.DetectedSeasonKey);

        var written = BundleWriter.Write(slot);
        Assert.True(written.Length > 0);

        var reparsed = BundleReader.Read(written, fileName);
        Assert.NotNull(reparsed.Metadata);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reparsed.Metadata!.AllProperties.Count());
    }

    [Fact]
    public void S3SlotBundle_ParsesWithMetadataOnly()
    {
        var file = TestDataHelper.GetPath("S3", "wd3_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        slot.DetectedSeasonKey = TestSeasons.Registry.DetectFromFileName(file)?.SeasonKey;
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Equal("s3", slot.DetectedSeasonKey);
    }

    [Fact]
    public void S3SlotBundle_RoundTripsNatively()
    {
        var file = TestDataHelper.GetPath("S3", "wd3_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());
    }

    [Fact]
    public void S4SlotBundle_ParsesWithChoiceStats()
    {
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        slot.DetectedSeasonKey = TestSeasons.Registry.DetectFromFileName(file)?.SeasonKey;
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.NotNull(slot.ChoiceStats);
        Assert.Equal("s4", slot.DetectedSeasonKey);
    }

    [Fact]
    public void S4SlotBundle_RoundTripsChoiceStats()
    {
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.ChoiceStats);
        Assert.Equal(
            slot.ChoiceStats!.AllProperties.Count(),
            reloaded.ChoiceStats!.AllProperties.Count());
    }

    [Fact]
    public void MichonneSlotBundle_ParsesWithMetadataOnly()
    {
        var file = TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle");
        var slot = BundleReader.Read(file);
        slot.DetectedSeasonKey = TestSeasons.Registry.DetectFromFileName(file)?.SeasonKey;
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Equal("michonne", slot.DetectedSeasonKey);
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
}
