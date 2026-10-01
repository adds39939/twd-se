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
using TwdSaveEditor.Season.S3.Choices;
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
    [InlineData("s1", "dougcarley_saved", "doug", "carley")]
    [InlineData("s2", "shot_kenny", "true", "false")]
    public void S1S2_CreateEditSaveReload(string season, string choiceKey, string value1, string value2)
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
    public void S3_CreateEditSaveReload_WithEventLog()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(Registry, tempDir);
            var slot = mgr.CreateNewSave("wd3_saveslot1.bundle", "s3", 2);

            Assert.True(File.Exists(slot.FilePath));
            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath));

            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded, S3ChoiceNodes.Map);
            Assert.True(accessor.HasEventLog);

            var allChoices = accessor.GetAllChoices();
            Assert.True(allChoices.Count > 0, "Expected choices from created S3 EventLog");

            var choiceDef = TestSeasons.ChoicesFor("s3").FirstOrDefault(c => c.Options.Length >= 2);
            if (choiceDef != null)
            {
                var originalVal = accessor.GetChoiceValue(choiceDef.ChoiceKey);
                var altOption = choiceDef.Options.First(o => o.Value != originalVal);
                accessor.SetChoiceValue(choiceDef.ChoiceKey, altOption.Value);

                accessor.InvalidateCache();
                Assert.Equal(altOption.Value, accessor.GetChoiceValue(choiceDef.ChoiceKey));
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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
        var s1Accessor = new SaveAccessor(s1.Choices!);
        s1Accessor.SetChoiceValue("dougcarley_saved", "carley");

        var s2 = Registry.CreateSave("s2", 1, "wd2_test.bundle");
        var s2Accessor = new SaveAccessor(s2.Choices!);

        foreach (var (key, value) in s1Accessor.GetAllChoices())
            s2Accessor.SetChoiceValue(key, value);

        Assert.Equal("carley", s2Accessor.GetChoiceValue("dougcarley_saved"));

        var written = BundleWriter.Write(s2);
        var reloaded = BundleReader.Read(written, "wd2_test.bundle");
        var reloadedAccessor = new SaveAccessor(reloaded.Choices!);
        Assert.Equal("carley", reloadedAccessor.GetChoiceValue("dougcarley_saved"));
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

        var playtime = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x7C725227A47FD1BA);
        Assert.NotNull(playtime);
        Assert.IsType<IntValue>(playtime.Value);

        var progress = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(progress);
        Assert.IsType<StringValue>(progress.Value);

        var autosave = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xF235E9FCE9562E01);
        Assert.NotNull(autosave);
        Assert.IsType<StringValue>(autosave.Value);
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

        Assert.True(slot.FileTable.Count > 2);

        var hashEntries = slot.FileTable.Where(f => f.Name.StartsWith("_hash_")).ToList();
        Assert.True(hashEntries.Count > 0, "Expected hash-only file table entries");

        var namedEntries = slot.FileTable.Where(f => !f.Name.StartsWith("_hash_")).ToList();
        Assert.Contains(namedEntries, f => f.Name == "metadata_save.p");
        Assert.Contains(namedEntries, f => f.Name == "default.save");
    }

    [Fact]
    public void AutosaveBundle_RoundTrips()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        var slot = BundleReader.Read(path);

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "_wd1_saveslot1_autosave.bundle");

        Assert.NotNull(reloaded.Metadata);
        Assert.Equal(slot.FileTable.Count, reloaded.FileTable.Count);
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
