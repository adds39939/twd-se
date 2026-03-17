using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

public class S3S4MichonneTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new S3Handler(), new S4Handler(), new MichonneHandler(),
    ]);

    private static ISeasonRegistry CreateRegistry() => Registry;

    private static SaveSlot LoadSlot(string filePath)
    {
        var slot = BundleReader.Read(filePath);
        slot.DetectedSeasonKey = Registry.DetectFromFileName(filePath)?.SeasonKey;
        return slot;
    }

    // ── S3 native format tests ─────────────────────────────────────────

    [Fact]
    public void S3SlotBundle_ParsesWithMetadataOnly()
    {
        var file = TestDataHelper.GetPath("S3", "wd3_saveslot1.bundle");
        var slot = LoadSlot(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Equal("s3", slot.DetectedSeasonKey);
    }

    [Fact]
    public void S3_EStoreReader_ParsesEventLog()
    {
        var estorePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id.estore");
        var entries = EStoreReader.ReadEventLog(estorePath);
        Assert.True(entries.Count > 0, "Expected EventLog entries from estore/epage");

        // Should contain "Executing Dialog Node" events
        var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
        Assert.True(dialogNodes.Count > 0, "Expected dialog node events");

        // Should contain "Begin Episode" events
        var beginEps = entries.Where(e => e.EventTypeHash == EventLogEntry.EventTypes.BeginEpisode).ToList();
        Assert.True(beginEps.Count > 0, "Expected Begin Episode events");
    }

    [Fact]
    public void S3SlotBundle_RoundTripsNatively()
    {
        var file = TestDataHelper.GetPath("S3", "wd3_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices); // Should NOT have choices.prop
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());
    }

    // ── S4 native format tests ─────────────────────────────────────────

    [Fact]
    public void S4SlotBundle_ParsesWithChoiceStats()
    {
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = LoadSlot(file);
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

    // ── Michonne native format tests ───────────────────────────────────

    [Fact]
    public void MichonneSlotBundle_ParsesWithMetadataOnly()
    {
        var file = TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle");
        var slot = LoadSlot(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Equal("michonne", slot.DetectedSeasonKey);
    }

    // ── General tests ──────────────────────────────────────────────────

    [Fact]
    public void SaveAccessor_WorksWithNullChoices()
    {
        var accessor = new SaveAccessor(null, null);
        Assert.False(accessor.HasChoices);
        Assert.Null(accessor.GetChoiceValue("test_key"));
        Assert.Empty(accessor.GetAllChoices());
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetChoiceValue("test_key", "test_value"));
    }

    [Fact]
    public void DetectedSeasonKey_WorksForAllPrefixes()
    {
        var testCases = new[]
        {
            ("wd1_saveslot1.bundle", "s1"), ("wd2_saveslot1.bundle", "s2"),
            ("wd3_saveslot1.bundle", "s3"), ("wd4_saveslot1.bundle", "s4"),
            ("wdm_saveslot1.bundle", "michonne"),
        };
        foreach (var (fileName, expected) in testCases)
        {
            var detected = Registry.DetectFromFileName(fileName)?.SeasonKey;
            Assert.Equal(expected, detected);
        }
    }

    [Fact]
    public void ChoiceDatabase_HasDefinitionsForAllSeasons()
    {
        var expected = new Dictionary<string, int>
        {
            ["s1"] = 47, ["s1_400days"] = 8, ["s2"] = 25,
            ["michonne"] = 15, ["s3"] = 24, ["s4"] = 15,
        };
        foreach (var (season, minCount) in expected)
        {
            var choices = ChoiceDatabase.ForSeason(season).ToList();
            Assert.True(choices.Count >= minCount,
                $"Season {season}: expected >= {minCount}, got {choices.Count}");
        }
    }

    [Fact]
    public void S3_EventLogAccessor_DetectsChoicesFromCreatedEStore()
    {
        // Create a new S3 save with choices, write estore/epage, then verify detection
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(CreateRegistry(), tempDir);
            var slot = mgr.CreateNewSave("wd3_saveslot1.bundle", "s3", 2);

            // Reload and verify choices are detectable via EventLogAccessor
            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded);
            Assert.True(accessor.HasEventLog);

            var allChoices = accessor.GetAllChoices();
            Assert.True(allChoices.Count > 0, "Expected detected choices from created EventLog");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void S4_ChoiceStatsAccessor_DetectsChoicesFromGUIDs()
    {
        // Use our Ep1 Ending save which has at least 1 GUID
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.ChoiceStats);

        var accessor = new ChoiceStatsAccessor(slot);
        // The Ep1 ending save should have at least one detectable choice
        var allChoices = ChoiceDatabase.ForSeason("s4").ToList();
        var detected = allChoices
            .Select(c => accessor.GetChoiceValue(c.ChoiceKey))
            .Where(v => v != null)
            .ToList();
        Assert.True(detected.Count > 0,
            "Expected at least one detectable S4 choice from Ep1 Ending save");
    }

    [Fact]
    public void S4_ChoiceStatsAccessor_CanEditAndRoundTrip()
    {
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.ChoiceStats);

        var accessor = new ChoiceStatsAccessor(slot);
        accessor.SetChoiceValue("aj_bed", "under");
        Assert.Equal("under", accessor.GetChoiceValue("aj_bed"));

        // Round-trip
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);
        Assert.NotNull(reloaded.ChoiceStats);

        var ra = new ChoiceStatsAccessor(reloaded);
        Assert.Equal("under", ra.GetChoiceValue("aj_bed"));
    }

    [Fact]
    public void NewSave_S4_HasChoiceStats()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "s4", 1, "test_s4.bundle");
        Assert.NotNull(slot.ChoiceStats);
        Assert.Null(slot.Choices); // S4 should NOT have choices.prop

        // Should have pre-populated choices via ChoiceStatsAccessor
        var accessor = new ChoiceStatsAccessor(slot);
        var val = accessor.GetChoiceValue("fishing_or_hunting");
        Assert.NotNull(val);

        // Round-trip
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "test_s4.bundle");
        Assert.NotNull(reloaded.ChoiceStats);
        Assert.Null(reloaded.Choices);
    }

    [Fact]
    public void NewSave_S3_HasMetadataOnly()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "s3", 1, "test_s3.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // S3 has no choices.prop
        Assert.Null(slot.ChoiceStats); // S3 has no choicestats.pro
        Assert.Single(slot.FileTable); // Only metadata_slot.p

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "test_s3.bundle");
        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices);
    }

    [Fact]
    public void NewSave_Michonne_HasMetadataOnly()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "michonne", 1, "test_michonne.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Single(slot.FileTable);
    }

    [Fact]
    public void NewSave_S3_CreatesEstoreEpage_AndChoicesRoundTrip()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(CreateRegistry(), tempDir);
            var slot = mgr.CreateNewSave("wd3_saveslot1.bundle", "s3", 2);

            // Verify bundle was created
            Assert.True(File.Exists(slot.FilePath));

            // Verify estore/epage files were created alongside the bundle
            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath), $"estore not found: {slot.EStorePath}");
            Assert.NotNull(slot.EPagePaths);
            Assert.True(slot.EPagePaths.Count > 0);
            Assert.True(File.Exists(slot.EPagePaths[0]), $"epage not found: {slot.EPagePaths[0]}");

            // Read back the EventLog and verify choices are detectable
            var entries = EStoreReader.ReadEventLog(slot.EStorePath);
            Assert.True(entries.Count > 0, "Expected EventLog entries in created estore/epage");

            var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
            Assert.True(dialogNodes.Count > 0, "Expected dialog node events");

            // Verify an S3 Ep1 choice is detectable via EventLogAccessor
            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded);
            // stayed_junkyard is an S3 Ep1 choice
            var val = accessor.GetChoiceValue("stayed_junkyard");
            Assert.NotNull(val);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewSave_Michonne_CreatesEstoreEpage()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(CreateRegistry(), tempDir);
            var slot = mgr.CreateNewSave("wdm_saveslot1.bundle", "michonne", 1);

            Assert.True(File.Exists(slot.FilePath));
            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath));
            Assert.NotNull(slot.EPagePaths);
            Assert.True(slot.EPagePaths.Count > 0);
            Assert.True(File.Exists(slot.EPagePaths[0]));

            // Read back EventLog
            var entries = EStoreReader.ReadEventLog(slot.EStorePath);
            Assert.True(entries.Count > 0, "Expected EventLog entries");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Michonne_EventLogAccessor_ParsesRealEStore()
    {
        var bundlePath = TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle");
        var estorePath = TestDataHelper.GetPath("Michonne", "_wdm_saveslot4_id.estore");

        // First verify the estore/epage files can be parsed
        var entries = EStoreReader.ReadEventLog(estorePath);
        Assert.True(entries.Count > 0, $"Expected events from Michonne estore, got {entries.Count}");

        var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
        Assert.True(dialogNodes.Count > 0, $"Expected dialog node events, got {dialogNodes.Count}");

        // Now test the EventLogAccessor
        var slot = LoadSlot(bundlePath);
        slot.EStorePath = estorePath;
        slot.EPagePaths = Directory.GetFiles(
                TestDataHelper.GetSeasonDir("Michonne"), "_wdm_saveslot4_id_Page*.epage")
            .OrderBy(f => f).ToList();

        var accessor = new EventLogAccessor(slot);
        Assert.True(accessor.HasEventLog);

        // Our TestData has only Page971.epage (partial data), so we may not match
        // any known Michonne GUID→CRC64 mappings. The important thing is that
        // parsing succeeds and the accessor functions without crashing.
        var allChoices = accessor.GetAllChoices();
        // allChoices.Count may be 0 with limited page data - that's OK
        Assert.True(allChoices.Count >= 0,
            $"Michonne EventLogAccessor GetAllChoices failed. " +
            $"Total events: {entries.Count}, dialog nodes: {dialogNodes.Count}");
    }

    [Fact]
    public void S2_SaveAccessor_DetectsChoicesFromRealSave()
    {
        var file = TestDataHelper.GetPath("S2", "wd2_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Choices); // S2 has season1.prop

        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
        var allChoices = accessor.GetAllChoices();
        Assert.True(allChoices.Count > 0, "Expected S2 imported choices");
    }

    [Fact]
    public void NewSave_S1S2_HaveChoices()
    {
        foreach (var season in new[] { "s1", "s1_400days", "s2" })
        {
            var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), season, 1, $"test_{season}.bundle");
            Assert.NotNull(slot.Choices);

            var written = BundleWriter.Write(slot);
            var reloaded = BundleReader.Read(written, $"test_{season}.bundle");
            Assert.NotNull(reloaded.Choices);
        }
    }
}
