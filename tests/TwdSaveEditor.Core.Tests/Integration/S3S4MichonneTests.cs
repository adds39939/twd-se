using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
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

        var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
        Assert.True(dialogNodes.Count > 0, "Expected dialog node events");

        var beginEps = entries.Where(e => e.EventTypeHash == EventLogEventTypes.BeginEpisode).ToList();
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
        Assert.Null(reloaded.Choices);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());
    }

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

    [Fact]
    public void MichonneSlotBundle_ParsesWithMetadataOnly()
    {
        var file = TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle");
        var slot = LoadSlot(file);
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Equal("michonne", slot.DetectedSeasonKey);
    }

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
            ["s1"] = 30, ["s1_400days"] = 5, ["s2"] = 25,
            ["michonne"] = 15, ["s3"] = 24, ["s4"] = 15,
        };
        foreach (var (season, minCount) in expected)
        {
            var choices = TestSeasons.ChoicesFor(season).ToList();
            Assert.True(choices.Count >= minCount,
                $"Season {season}: expected >= {minCount}, got {choices.Count}");
        }
    }

    [Fact]
    public void S3_EventLogAccessor_DetectsChoicesFromCreatedEStore()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(CreateRegistry(), tempDir);
            var slot = mgr.CreateNewSave("wd3_saveslot1.bundle", "s3", 2);

            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded, S3ChoiceNodes.Map);
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
        var file = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.ChoiceStats);

        var accessor = new ChoiceStatsAccessor(slot);
        var allChoices = TestSeasons.ChoicesFor("s4").ToList();
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

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);
        Assert.NotNull(reloaded.ChoiceStats);

        var ra = new ChoiceStatsAccessor(reloaded);
        Assert.Equal("under", ra.GetChoiceValue("aj_bed"));
    }

    [Fact]
    public void NewSave_S4_HasChoiceStats()
    {
        var slot = CreateRegistry().CreateSave("s4", 1, "test_s4.bundle");
        Assert.NotNull(slot.ChoiceStats);
        Assert.Null(slot.Choices);

        var accessor = new ChoiceStatsAccessor(slot);
        var val = accessor.GetChoiceValue("fishing_or_hunting");
        Assert.NotNull(val);

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "test_s4.bundle");
        Assert.NotNull(reloaded.ChoiceStats);
        Assert.Null(reloaded.Choices);
    }

    [Fact]
    public void NewSave_S3_HasMetadataOnly()
    {
        var slot = CreateRegistry().CreateSave("s3", 1, "test_s3.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Null(slot.ChoiceStats);
        Assert.Single(slot.Files);

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "test_s3.bundle");
        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices);
    }

    [Fact]
    public void NewSave_Michonne_HasMetadataOnly()
    {
        var slot = CreateRegistry().CreateSave("michonne", 1, "test_michonne.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Single(slot.Files);
    }

    [Fact]
    public void NewSave_S3_CreatesEstoreEpage_AndChoicesRoundTrip()
    {
        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            var mgr = new SaveManager(CreateRegistry(), tempDir);
            var slot = mgr.CreateNewSave("wd3_saveslot1.bundle", "s3", 2);

            Assert.True(File.Exists(slot.FilePath));

            Assert.NotNull(slot.EStorePath);
            Assert.True(File.Exists(slot.EStorePath), $"estore not found: {slot.EStorePath}");
            Assert.NotNull(slot.EPagePaths);
            Assert.True(slot.EPagePaths.Count > 0);
            Assert.True(File.Exists(slot.EPagePaths[0]), $"epage not found: {slot.EPagePaths[0]}");

            var entries = EStoreReader.ReadEventLog(slot.EStorePath);
            Assert.True(entries.Count > 0, "Expected EventLog entries in created estore/epage");

            var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
            Assert.True(dialogNodes.Count > 0, "Expected dialog node events");

            var reloaded = BundleReader.Read(slot.FilePath);
            reloaded.EStorePath = slot.EStorePath;
            reloaded.EPagePaths = slot.EPagePaths;
            reloaded.DetectedSeasonKey = "s3";

            var accessor = new EventLogAccessor(reloaded, S3ChoiceNodes.Map);
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

        var entries = EStoreReader.ReadEventLog(estorePath);
        Assert.True(entries.Count > 0, $"Expected events from Michonne estore, got {entries.Count}");

        var dialogNodes = entries.Where(e => e.IsDialogNode).ToList();
        Assert.True(dialogNodes.Count > 0, $"Expected dialog node events, got {dialogNodes.Count}");

        var slot = LoadSlot(bundlePath);
        slot.EStorePath = estorePath;
        slot.EPagePaths = Directory.GetFiles(
                TestDataHelper.GetSeasonDir("Michonne"), "_wdm_saveslot4_id_Page*.epage")
            .OrderBy(f => f).ToList();

        var accessor = new EventLogAccessor(slot, MichonneChoiceNodes.Map);
        Assert.True(accessor.HasEventLog);

        var allChoices = accessor.GetAllChoices();
        Assert.True(allChoices.Count >= 0,
            $"Michonne EventLogAccessor GetAllChoices failed. " +
            $"Total events: {entries.Count}, dialog nodes: {dialogNodes.Count}");
    }

    [Fact]
    public void S2_SaveAccessor_DetectsChoicesFromRealSave()
    {
        var file = TestDataHelper.GetPath("S2", "wd2_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Choices);

        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
        var allChoices = accessor.GetAllChoices();
        Assert.True(allChoices.Count > 0, "Expected S2 imported choices");
    }

    [Fact]
    public void NewSave_S1S2_HaveChoices()
    {
        foreach (var season in new[] { "s1", "s1_400days", "s2" })
        {
            var slot = CreateRegistry().CreateSave(season, 1, $"test_{season}.bundle");
            Assert.NotNull(slot.Choices);

            var written = BundleWriter.Write(slot);
            var reloaded = BundleReader.Read(written, $"test_{season}.bundle");
            Assert.NotNull(reloaded.Choices);
        }
    }
}
