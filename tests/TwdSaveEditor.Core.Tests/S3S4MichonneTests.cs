using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

public class S3S4MichonneTests
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    // ── S3 native format tests ─────────────────────────────────────────

    [Fact]
    public void S3SlotBundle_ParsesWithMetadataOnly()
    {
        var file = Path.Combine(SaveDir, "S3", "Episode 1", "wd3_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // S3 doesn't have choices.prop (native)
        Assert.Equal("s3", slot.DetectedSeasonKey);
    }

    [Fact]
    public void S3_EStoreReader_ParsesEventLog()
    {
        var estorePath = Path.Combine(SaveDir, "S3", "Episode 1", "_wd3_saveslot1_id.estore");
        if (!File.Exists(estorePath)) return;

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
    public void S3_EStoreReader_MoreEventsInLaterEpisodes()
    {
        var ep1Store = Path.Combine(SaveDir, "S3", "Episode 1", "_wd3_saveslot1_id.estore");
        var ep5Store = Path.Combine(SaveDir, "S3", "Episode 5", "The end", "_wd3_saveslot1_id.estore");
        if (!File.Exists(ep1Store) || !File.Exists(ep5Store)) return;

        var ep1Entries = EStoreReader.ReadEventLog(ep1Store);
        var ep5Entries = EStoreReader.ReadEventLog(ep5Store);

        // Episode 5 should have more events than Episode 1
        Assert.True(ep5Entries.Count > ep1Entries.Count,
            $"Ep5 ({ep5Entries.Count}) should have more events than Ep1 ({ep1Entries.Count})");
    }

    [Fact]
    public void S3SlotBundle_RoundTripsNatively()
    {
        var file = Path.Combine(SaveDir, "S3", "Episode 1", "wd3_saveslot1.bundle");
        if (!File.Exists(file)) return;

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
        var file = Path.Combine(SaveDir, "S4", "Episode 1", "Ending", "wd4_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // S4 doesn't have choices.prop
        Assert.NotNull(slot.ChoiceStats); // S4 has choicestats.pro (GUIDs)
        Assert.Equal("s4", slot.DetectedSeasonKey);
    }

    [Fact]
    public void S4SlotBundle_RoundTripsChoiceStats()
    {
        var file = Path.Combine(SaveDir, "S4", "Episode 1", "Ending", "wd4_saveslot1.bundle");
        if (!File.Exists(file)) return;

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
        var file = Path.Combine(SaveDir, "Michonne", "wdm_saveslot2.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // Michonne doesn't have choices.prop
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
        accessor.SetChoiceValue("test_key", "test_value");
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
            var slot = new SaveSlot
            {
                FilePath = fileName, FileName = fileName,
                OuterHeader = new MetaStreamHeader(), FileTable = [],
            };
            Assert.Equal(expected, slot.DetectedSeasonKey);
        }
    }

    [Fact]
    public void AllSlotBundles_ParseWithoutCrash()
    {
        if (!Directory.Exists(SaveDir)) return;
        var slotBundles = Directory.GetFiles(SaveDir, "wd*.bundle", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('_')).ToList();
        foreach (var file in slotBundles)
        {
            var slot = BundleReader.Read(file);
            Assert.NotNull(slot.Metadata);
            Assert.NotNull(slot.DetectedSeasonKey);
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
    public void S3_EventLogAccessor_DetectsChoicesFromEStore()
    {
        // Use Ep5 "The end" save which should have all choices made
        var bundlePath = Path.Combine(SaveDir, "S3", "Episode 5", "The end", "wd3_saveslot1.bundle");
        var estorePath = Path.Combine(SaveDir, "S3", "Episode 5", "The end", "_wd3_saveslot1_id.estore");
        if (!File.Exists(bundlePath) || !File.Exists(estorePath)) return;

        var slot = BundleReader.Read(bundlePath);
        slot.EStorePath = estorePath;
        slot.EPagePaths = Directory.GetFiles(
            Path.GetDirectoryName(estorePath)!, "_wd3_saveslot1_id_Page*.epage")
            .OrderBy(f => f).ToList();

        var accessor = new EventLogAccessor(slot);
        Assert.True(accessor.HasEventLog);

        // The Ep5 save should have choices resolved
        var allChoices = accessor.GetAllChoices();
        Assert.True(allChoices.Count > 0, "Expected detected choices from EventLog");

        // Check a known choice: shot_conrad should be resolved
        var conradValue = accessor.GetChoiceValue("shot_conrad");
        Assert.NotNull(conradValue); // Should be "true" or "false"
    }

    [Fact]
    public void NewSave_S1S2_HaveChoices()
    {
        foreach (var season in new[] { "s1", "s1_400days", "s2" })
        {
            var slot = SaveSlotFactory.CreateForSeason(season, 1, $"test_{season}.bundle");
            Assert.NotNull(slot.Choices);

            var written = BundleWriter.Write(slot);
            var reloaded = BundleReader.Read(written, $"test_{season}.bundle");
            Assert.NotNull(reloaded.Choices);
        }
    }
}
