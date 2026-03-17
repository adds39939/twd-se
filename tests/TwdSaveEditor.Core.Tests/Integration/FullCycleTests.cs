using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;

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
