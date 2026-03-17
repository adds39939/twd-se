using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Tests that S3, S4, and Michonne save files load correctly.
/// </summary>
public class S3S4MichonneTests
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    [Fact]
    public void S3SlotBundle_ParsesSuccessfully()
    {
        var file = Path.Combine(SaveDir, "S3", "Episode 1", "wd3_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);

        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // S3 doesn't have choices.prop
        Assert.Equal("s3", slot.DetectedSeasonKey);
        Assert.True(slot.FileTable.Count > 0);
    }

    [Fact]
    public void S4SlotBundle_ParsesWithChoiceStats()
    {
        var file = Path.Combine(SaveDir, "S4", "Episode 1", "Ending", "wd4_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);

        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // S4 doesn't have choices.prop
        Assert.NotNull(slot.ChoiceStats); // S4 has choicestats.pro
        Assert.Equal("s4", slot.DetectedSeasonKey);

        // ChoiceStats should have the GUID string
        var allProps = slot.ChoiceStats.AllProperties.ToList();
        Assert.True(allProps.Count > 0);
        Assert.IsType<StringValue>(allProps[0].Value);
    }

    [Fact]
    public void MichonneSlotBundle_ParsesSuccessfully()
    {
        var file = Path.Combine(SaveDir, "Michonne", "wdm_saveslot2.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);

        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices); // Michonne doesn't have choices.prop
        Assert.Equal("michonne", slot.DetectedSeasonKey);
    }

    [Fact]
    public void SaveAccessor_WorksWithNullChoices()
    {
        var accessor = new SaveAccessor(null, null);

        Assert.False(accessor.HasChoices);
        Assert.Null(accessor.GetChoiceValue("test_key"));
        Assert.Empty(accessor.GetAllChoices());

        // Should not throw
        accessor.SetChoiceValue("test_key", "test_value");
    }

    [Fact]
    public void SaveAccessor_DetectCurrentChoice_ReturnsMinusOneWhenNoChoices()
    {
        var accessor = new SaveAccessor(null, null);
        var choice = new ChoiceDefinition
        {
            SeasonKey = "s3",
            Episode = 1,
            Description = "Test",
            ChoiceKey = "test_key",
            Options = [new ChoiceOption { Label = "A", Value = "a" }],
            Category = "Major",
        };

        Assert.Equal(-1, accessor.DetectCurrentChoice(choice));
    }

    [Fact]
    public void DetectedSeasonKey_WorksForAllPrefixes()
    {
        var testCases = new[]
        {
            ("wd1_saveslot1.bundle", "s1"),
            ("wd2_saveslot1.bundle", "s2"),
            ("wd3_saveslot1.bundle", "s3"),
            ("wd4_saveslot1.bundle", "s4"),
            ("wdm_saveslot1.bundle", "michonne"),
            ("_wd3_saveslot1_autosave.bundle", "s3"),
            ("_wdm_saveslot2_checkpoint1.bundle", "michonne"),
        };

        foreach (var (fileName, expectedSeason) in testCases)
        {
            var slot = new SaveSlot
            {
                FilePath = fileName,
                FileName = fileName,
                OuterHeader = new MetaStreamHeader(),
                FileTable = [],
            };

            Assert.Equal(expectedSeason, slot.DetectedSeasonKey);
        }
    }

    [Fact]
    public void S3S4Michonne_AllSlotBundles_ParseWithoutCrash()
    {
        if (!Directory.Exists(SaveDir)) return;

        var slotBundles = Directory.GetFiles(SaveDir, "wd*.bundle", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .ToList();

        foreach (var file in slotBundles)
        {
            var slot = BundleReader.Read(file);
            Assert.NotNull(slot.Metadata);
            Assert.NotNull(slot.DetectedSeasonKey);
        }
    }

    [Fact]
    public void S4SlotBundle_RoundTrips()
    {
        var file = Path.Combine(SaveDir, "S4", "Episode 1", "Ending", "wd4_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.Metadata);
        Assert.NotNull(reloaded.ChoiceStats);

        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());

        Assert.Equal(
            slot.ChoiceStats!.AllProperties.Count(),
            reloaded.ChoiceStats!.AllProperties.Count());
    }

    [Fact]
    public void S4ChoiceStats_CanModifyGuidsAndRoundTrip()
    {
        var file = Path.Combine(SaveDir, "S4", "Episode 1", "Ending", "wd4_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.ChoiceStats);

        // Read original GUID string
        var prop = slot.ChoiceStats!.AllProperties.First();
        var original = ((StringValue)prop.Value).Value;
        Assert.Contains("{", original);

        // Modify the GUID string (add a new GUID)
        var modified = original + "\t( {AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE} )";
        prop.Value = new StringValue(modified);

        // Round-trip
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.ChoiceStats);
        var reloadedProp = reloaded.ChoiceStats!.AllProperties.First();
        var reloadedValue = ((StringValue)reloadedProp.Value).Value;
        Assert.Contains("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", reloadedValue);
    }

    [Fact]
    public void S3SlotBundle_RoundTripsWithoutChoices()
    {
        var file = Path.Combine(SaveDir, "S3", "Episode 1", "wd3_saveslot1.bundle");
        if (!File.Exists(file)) return;

        var slot = BundleReader.Read(file);
        Assert.Null(slot.Choices);

        // Round-trip should preserve metadata
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, file);

        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices); // Should stay null, no injection
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());
    }

    [Fact]
    public void ChoiceDatabase_HasDefinitionsForAllSeasons()
    {
        var seasons = new[] { "s1", "s1_400days", "s2", "michonne", "s3", "s4" };
        foreach (var season in seasons)
        {
            var choices = ChoiceDatabase.ForSeason(season).ToList();
            Assert.True(choices.Count > 0, $"No choices defined for season {season}");
        }
    }
}
