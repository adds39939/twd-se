using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Base.Services;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Choices;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Accessors;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Core.Tests.Integration;

public class FullCycleTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new S3Handler(), new S4Handler(), new MichonneHandler(),
    ]);

    [Theory]
    [InlineData("s2", "shot_kenny", "true", "false")]
    public void S2_CreateEditSaveReload(string season, string choiceKey, string value1, string value2)
    {
        var slot = Registry.CreateSave(season, 5, "test.bundle");
        var accessor = new SaveAccessor(slot.Choices!);

        accessor.SetChoiceValue(choiceKey, value1);
        Assert.Equal(value1, accessor.GetChoiceValue(choiceKey));

        var bytes = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(bytes, "test.bundle");
        var reloadedAccessor = new SaveAccessor(reloaded.Choices!);
        Assert.Equal(value1, reloadedAccessor.GetChoiceValue(choiceKey));

        reloadedAccessor.SetChoiceValue(choiceKey, value2);
        var bytes2 = BundleWriter.Write(reloaded);
        var reloaded2 = BundleReader.Read(bytes2, "test.bundle");
        var accessor2 = new SaveAccessor(reloaded2.Choices!);
        Assert.Equal(value2, accessor2.GetChoiceValue(choiceKey));
    }

    [Fact]
    public void S4_CreateEditSaveReload_WithChoiceStats()
    {
        var slot = Registry.CreateSave("s4", 1, "test_s4.bundle");
        Assert.NotNull(slot.ChoiceStats);

        var accessor = new ChoiceStatsAccessor(slot);

        accessor.SetChoiceValue("aj_bed", "under");
        Assert.Equal("under", accessor.GetChoiceValue("aj_bed"));

        var bytes = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(bytes, "test_s4.bundle");
        Assert.NotNull(reloaded.ChoiceStats);

        var reloadedAccessor = new ChoiceStatsAccessor(reloaded);
        Assert.Equal("under", reloadedAccessor.GetChoiceValue("aj_bed"));

        reloadedAccessor.SetChoiceValue("aj_bed", "on");
        var bytes2 = BundleWriter.Write(reloaded);
        var reloaded2 = BundleReader.Read(bytes2, "test_s4.bundle");
        var accessor2 = new ChoiceStatsAccessor(reloaded2);
        Assert.Equal("on", accessor2.GetChoiceValue("aj_bed"));
    }

    [Fact]
    public void Michonne_CreateEditSaveReload_WithEventLog()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(Registry, tempDir);
            var slot = mgr.CreateNewSave("wdm_saveslot1.bundle", "michonne", 1);

            Assert.True(File.Exists(slot.FilePath));
            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath));

            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "michonne";

            var accessor = new EventLogAccessor(reloaded, MichonneChoiceNodes.Map);
            Assert.True(accessor.HasEventLog);

            var allChoices = accessor.GetAllChoices();
            Assert.True(allChoices.Count > 0, "Expected choices from created Michonne EventLog");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void S1ToS2_ChoiceImport_CopiesAllChoices()
    {
        var s1 = Registry.CreateSave("s1", 3, "wd1_test.bundle");
        s1.DetectedSeasonKey = "s1";
        var s1Accessor = Registry.Get("s1")!.CreateChoiceAccessor(s1)!;
        s1Accessor.SetChoiceValue("DougCarley Saved", "doug");

        var s2 = Registry.CreateSave("s2", 1, "wd2_test.bundle");
        var s2Handler = Registry.Get("s2")!;
        ((IChoiceImporter)s2Handler).ImportChoices(s1, s2);

        Assert.Equal("doug", s2Handler.CreateChoiceAccessor(s2)!.GetChoiceValue("DougCarley Saved"));

        var written = BundleWriter.Write(s2);
        var reloaded = BundleReader.Read(written, "wd2_test.bundle");
        Assert.Equal("doug", reloaded.Choices!.GetString("DougCarley Saved"));
        Assert.NotNull(reloaded.Choices.Find("ChoiceTracker - 101"));
    }

    [Theory]
    [InlineData("follow_violet_louis", "louis")]
    [InlineData("violetlouis_saved", "louis")]
    public void S4_LouisPreset_SetsCorrectValues(string choiceKey, string expectedValue)
    {
        var slot = Registry.CreateSave("s4", 2, "wd4_test.bundle");
        var accessor = new ChoiceStatsAccessor(slot);

        accessor.SetChoiceValue("follow_violet_louis", "louis");
        accessor.SetChoiceValue("violetlouis_saved", "louis");

        Assert.Equal(expectedValue, accessor.GetChoiceValue(choiceKey));

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "wd4_test.bundle");
        var ra = new ChoiceStatsAccessor(reloaded);
        Assert.Equal(expectedValue, ra.GetChoiceValue(choiceKey));
    }

    [Theory]
    [InlineData("follow_violet_louis", "violet")]
    [InlineData("violetlouis_saved", "violet")]
    public void S4_VioletPreset_SetsCorrectValues(string choiceKey, string expectedValue)
    {
        var slot = Registry.CreateSave("s4", 2, "wd4_test.bundle");
        var accessor = new ChoiceStatsAccessor(slot);

        accessor.SetChoiceValue("follow_violet_louis", "violet");
        accessor.SetChoiceValue("violetlouis_saved", "violet");

        Assert.Equal(expectedValue, accessor.GetChoiceValue(choiceKey));
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
        slot.DetectedSeasonKey = Registry.DetectFromFileName(fileName)?.SeasonKey;

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
}
