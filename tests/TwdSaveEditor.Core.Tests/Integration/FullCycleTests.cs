using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Model;

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
        // Create a new save with defaults
        var slot = SaveSlotFactory.CreateForSeason(Registry, season, 5, "test.bundle");
        var accessor = new SaveAccessor(slot.Choices!);

        // Set to value1
        accessor.SetChoiceValue(choiceKey, value1);
        Assert.Equal(value1, accessor.GetChoiceValue(choiceKey));

        // Write and reload
        var bytes = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(bytes, "test.bundle");
        var reloadedAccessor = new SaveAccessor(reloaded.Choices!);
        Assert.Equal(value1, reloadedAccessor.GetChoiceValue(choiceKey));

        // Set to value2 and round-trip again
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

            // Verify the save was created with estore/epage
            Assert.True(File.Exists(slot.FilePath));
            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath));

            // Reload and verify choices via EventLogAccessor
            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded);
            Assert.True(accessor.HasEventLog);

            // An S3 Ep1 choice should be present
            var allChoices = accessor.GetAllChoices();
            Assert.True(allChoices.Count > 0, "Expected choices from created S3 EventLog");

            // Modify a choice and verify it sticks
            var choiceDef = ChoiceDatabase.ForSeason("s3").FirstOrDefault(c => c.Options.Length >= 2);
            if (choiceDef != null)
            {
                var originalVal = accessor.GetChoiceValue(choiceDef.ChoiceKey);
                var altOption = choiceDef.Options.First(o => o.Value != originalVal);
                accessor.SetChoiceValue(choiceDef.ChoiceKey, altOption.Value);

                // Re-read to verify
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
        // Create
        var slot = SaveSlotFactory.CreateForSeason(Registry, "s4", 1, "test_s4.bundle");
        Assert.NotNull(slot.ChoiceStats);

        var accessor = new ChoiceStatsAccessor(slot);

        // Edit
        accessor.SetChoiceValue("aj_bed", "under");
        Assert.Equal("under", accessor.GetChoiceValue("aj_bed"));

        // Write and reload
        var bytes = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(bytes, "test_s4.bundle");
        Assert.NotNull(reloaded.ChoiceStats);

        var reloadedAccessor = new ChoiceStatsAccessor(reloaded);
        Assert.Equal("under", reloadedAccessor.GetChoiceValue("aj_bed"));

        // Edit again
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

            // Reload and verify choices via EventLogAccessor
            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "michonne";

            var accessor = new EventLogAccessor(reloaded);
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
        // Create an S1 save with choices
        var s1 = SaveSlotFactory.CreateForSeason(Registry, "s1", 3, "wd1_test.bundle");
        var s1Accessor = new SaveAccessor(s1.Choices!);
        s1Accessor.SetChoiceValue("dougcarley_saved", "carley");

        // Create an S2 save
        var s2 = SaveSlotFactory.CreateForSeason(Registry, "s2", 1, "wd2_test.bundle");
        var s2Accessor = new SaveAccessor(s2.Choices!);

        // Import S1 choices into S2
        foreach (var (key, value) in s1Accessor.GetAllChoices())
            s2Accessor.SetChoiceValue(key, value);

        // Verify the imported choice exists in S2
        Assert.Equal("carley", s2Accessor.GetChoiceValue("dougcarley_saved"));

        // Round-trip: write S2, read back, verify choices survived
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
        var slot = SaveSlotFactory.CreateForSeason(Registry, "s4", 2, "wd4_test.bundle");
        var accessor = new ChoiceStatsAccessor(slot);

        // Apply Louis preset choices
        accessor.SetChoiceValue("follow_violet_louis", "louis");
        accessor.SetChoiceValue("violetlouis_saved", "louis");

        Assert.Equal(expectedValue, accessor.GetChoiceValue(choiceKey));

        // Round-trip
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
        var slot = SaveSlotFactory.CreateForSeason(Registry, "s4", 2, "wd4_test.bundle");
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

        // Should have playtime property
        var playtime = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x7C725227A47FD1BA);
        Assert.NotNull(playtime);
        Assert.IsType<IntValue>(playtime.Value);

        // Should have episode progress
        var progress = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(progress);
        Assert.IsType<StringValue>(progress.Value);

        // Should have autosave file
        var autosave = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xF235E9FCE9562E01);
        Assert.NotNull(autosave);
        Assert.IsType<StringValue>(autosave.Value);
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

        // Verify round-trip doesn't crash
        var written = BundleWriter.Write(slot);
        Assert.True(written.Length > 0);

        var reparsed = BundleReader.Read(written, fileName);
        Assert.NotNull(reparsed.Metadata);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reparsed.Metadata!.AllProperties.Count());
    }
}
