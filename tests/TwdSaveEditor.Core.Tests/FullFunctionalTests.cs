using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Comprehensive functional tests that verify loading, editing, round-tripping,
/// and new-save creation across all seasons of TWD: The Telltale Definitive Series.
/// </summary>
public class FullFunctionalTests
{
    private const string SavesRoot = @"C:\Users\Adam\Downloads\twd-saves";

    private static readonly Dictionary<string, string> SeasonDirs = new()
    {
        ["s1"] = Path.Combine(SavesRoot, "S1"),
        ["s2"] = Path.Combine(SavesRoot, "S2"),
        ["s3"] = Path.Combine(SavesRoot, "S3"),
        ["s4"] = Path.Combine(SavesRoot, "S4"),
        ["michonne"] = Path.Combine(SavesRoot, "Michonne"),
    };

    private static readonly Dictionary<string, string> SeasonPrefixes = new()
    {
        ["s1"] = "wd1_",
        ["s2"] = "wd2_",
        ["s3"] = "wd3_",
        ["s4"] = "wd4_",
        ["michonne"] = "wdm_",
    };

    /// <summary>
    /// Recursively find all .bundle files under the saves root directory.
    /// </summary>
    private static List<string> FindAllBundles()
    {
        return Directory.GetFiles(SavesRoot, "*.bundle", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Determine expected season key from file path (based on parent directory).
    /// </summary>
    private static string? ExpectedSeasonFromPath(string filePath)
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(filePath))!;
        return dir switch
        {
            "S1" => "s1",
            "S2" => "s2",
            "S3" => "s3",
            "S4" => "s4",
            "Michonne" => "michonne",
            _ => null,
        };
    }

    /// <summary>
    /// Returns true if the file is a slot bundle (not a checkpoint/autosave).
    /// Slot bundles do NOT start with underscore.
    /// </summary>
    private static bool IsSlotBundle(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return !name.StartsWith('_');
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 1: ALL save files load without errors
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllSaveFilesLoadWithoutErrors()
    {
        var log = new List<string>();
        var failures = new List<string>();
        var seasonCounts = new Dictionary<string, int>();

        var bundles = FindAllBundles();
        log.Add($"Found {bundles.Count} total .bundle files");
        log.Add("");

        foreach (var file in bundles)
        {
            var fileName = Path.GetFileName(file);
            var expectedSeason = ExpectedSeasonFromPath(file);
            var isSlot = IsSlotBundle(file);

            try
            {
                var slot = BundleReader.Read(file);

                // Track per-season counts
                var season = slot.DetectedSeasonKey ?? "unknown";
                seasonCounts[season] = seasonCounts.GetValueOrDefault(season) + 1;

                // Verify DetectedSeasonKey matches expected
                if (expectedSeason != null && slot.DetectedSeasonKey != expectedSeason)
                {
                    failures.Add($"SEASON MISMATCH: {fileName} expected={expectedSeason} detected={slot.DetectedSeasonKey}");
                }

                // Slot bundles must have metadata
                if (isSlot && slot.Metadata == null)
                {
                    failures.Add($"MISSING METADATA (slot): {fileName}");
                }

                var hasChoices = slot.Choices != null;
                var hasChoiceStats = slot.ChoiceStats != null;
                var hasMetadata = slot.Metadata != null;

                log.Add($"  {fileName,-55} season={season,-10} meta={hasMetadata,-5} choices={hasChoices,-5} choiceStats={hasChoiceStats,-5}");
            }
            catch (Exception ex)
            {
                failures.Add($"LOAD FAILED: {fileName} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        log.Add("");
        log.Add("--- Per-season counts ---");
        foreach (var kvp in seasonCounts.OrderBy(k => k.Key))
        {
            log.Add($"  {kvp.Key}: {kvp.Value} files");
        }

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics even on success
        Assert.Fail(string.Join(Environment.NewLine, log));
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 2: S1/S2 saves have editable choices
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void S1S2SlotBundlesHaveEditableChoices()
    {
        var log = new List<string>();
        var failures = new List<string>();

        var s1Files = Directory.GetFiles(SeasonDirs["s1"], "*.bundle", SearchOption.AllDirectories)
            .Where(IsSlotBundle).ToList();
        var s2Files = Directory.GetFiles(SeasonDirs["s2"], "*.bundle", SearchOption.AllDirectories)
            .Where(IsSlotBundle).ToList();

        log.Add($"S1 slot bundles: {s1Files.Count}");
        log.Add($"S2 slot bundles: {s2Files.Count}");
        log.Add("");

        foreach (var file in s1Files.Concat(s2Files))
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var slot = BundleReader.Read(file);
                var accessor = new SaveAccessor(slot.Choices, slot.Metadata);

                if (!accessor.HasChoices)
                {
                    failures.Add($"NO CHOICES: {fileName}");
                    continue;
                }

                var allChoices = accessor.GetAllChoices();
                if (allChoices.Count == 0)
                {
                    failures.Add($"EMPTY CHOICES: {fileName}");
                    continue;
                }

                log.Add($"  {fileName} -> {allChoices.Count} choices:");
                foreach (var (key, value) in allChoices)
                {
                    log.Add($"    {key} = {value}");
                }
                log.Add("");
            }
            catch (Exception ex)
            {
                failures.Add($"ERROR: {fileName} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics
        Assert.Fail(string.Join(Environment.NewLine, log));
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 3: S4 saves have ChoiceStats with parseable GUIDs that round-trip
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void S4SlotBundlesHaveChoiceStatsThatRoundTrip()
    {
        var log = new List<string>();
        var failures = new List<string>();

        var s4SlotFiles = Directory.GetFiles(SeasonDirs["s4"], "*.bundle", SearchOption.AllDirectories)
            .Where(IsSlotBundle).ToList();

        log.Add($"S4 slot bundles: {s4SlotFiles.Count}");
        log.Add("");

        int withChoiceStats = 0;

        foreach (var file in s4SlotFiles)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var slot = BundleReader.Read(file);

                if (slot.ChoiceStats == null)
                {
                    log.Add($"  {fileName} -> no ChoiceStats (skipping)");
                    continue;
                }

                withChoiceStats++;

                // Verify GUID strings are parseable
                foreach (var prop in slot.ChoiceStats.AllProperties)
                {
                    if (prop.Value is StringValue sv)
                    {
                        if (!Guid.TryParse(sv.Value, out _))
                        {
                            // Not all string values are GUIDs, just log
                            log.Add($"    {fileName} non-GUID string: {sv.Value}");
                        }
                    }
                }

                // Round-trip test: Write then Read back
                var writtenBytes = BundleWriter.Write(slot);
                var reloaded = BundleReader.Read(writtenBytes, file);

                if (reloaded.ChoiceStats == null)
                {
                    failures.Add($"ROUND-TRIP LOST ChoiceStats: {fileName}");
                    continue;
                }

                // Compare property counts
                var origCount = slot.ChoiceStats.AllProperties.Count();
                var reloadedCount = reloaded.ChoiceStats.AllProperties.Count();
                if (origCount != reloadedCount)
                {
                    failures.Add($"ROUND-TRIP PROP COUNT MISMATCH: {fileName} orig={origCount} reloaded={reloadedCount}");
                    continue;
                }

                log.Add($"  {fileName} -> ChoiceStats round-trip OK ({origCount} props)");
            }
            catch (Exception ex)
            {
                failures.Add($"ERROR: {fileName} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        log.Add("");
        log.Add($"S4 slots with ChoiceStats: {withChoiceStats}/{s4SlotFiles.Count}");

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics
        Assert.Fail(string.Join(Environment.NewLine, log));
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 4: S3/Michonne saves load gracefully without choices, round-trip
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void S3MichonneSlotBundlesLoadGracefullyAndRoundTrip()
    {
        var log = new List<string>();
        var failures = new List<string>();

        var s3Files = Directory.GetFiles(SeasonDirs["s3"], "*.bundle", SearchOption.AllDirectories)
            .Where(IsSlotBundle).ToList();
        var michonneFiles = Directory.GetFiles(SeasonDirs["michonne"], "*.bundle", SearchOption.AllDirectories)
            .Where(IsSlotBundle).ToList();

        log.Add($"S3 slot bundles: {s3Files.Count}");
        log.Add($"Michonne slot bundles: {michonneFiles.Count}");
        log.Add("");

        foreach (var file in s3Files.Concat(michonneFiles))
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var slot = BundleReader.Read(file);

                // Verify Choices is null (no choices.prop in S3/Michonne)
                if (slot.Choices != null)
                {
                    // This might actually be valid for some saves; log it
                    log.Add($"  {fileName} -> UNEXPECTED: has Choices (not null)");
                }

                // Verify Metadata is not null
                if (slot.Metadata == null)
                {
                    failures.Add($"MISSING METADATA: {fileName}");
                    continue;
                }

                // Round-trip: Write then Read back, verify metadata preserved
                var writtenBytes = BundleWriter.Write(slot);
                var reloaded = BundleReader.Read(writtenBytes, file);

                if (reloaded.Metadata == null)
                {
                    failures.Add($"ROUND-TRIP LOST METADATA: {fileName}");
                    continue;
                }

                // Compare metadata property counts
                var origMetaCount = slot.Metadata.AllProperties.Count();
                var reloadedMetaCount = reloaded.Metadata.AllProperties.Count();
                if (origMetaCount != reloadedMetaCount)
                {
                    failures.Add($"ROUND-TRIP METADATA PROP COUNT MISMATCH: {fileName} orig={origMetaCount} reloaded={reloadedMetaCount}");
                    continue;
                }

                log.Add($"  {fileName} -> OK (metadata={origMetaCount} props, choices=null, round-trip OK)");
            }
            catch (Exception ex)
            {
                failures.Add($"ERROR: {fileName} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics
        Assert.Fail(string.Join(Environment.NewLine, log));
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 5: New save creation works for ALL seasons
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void NewSaveCreationWorksForAllSeasons()
    {
        var log = new List<string>();
        var failures = new List<string>();

        var seasonKeys = new[] { "s1", "s1_400days", "s2", "michonne", "s3", "s4" };

        foreach (var seasonKey in seasonKeys)
        {
            var fileName = $"test_{seasonKey}.bundle";
            try
            {
                var slot = SaveSlotFactory.CreateForSeason(seasonKey, 1, fileName);

                // Verify valid structure
                Assert.NotNull(slot);
                Assert.NotNull(slot.OuterHeader);
                Assert.NotNull(slot.FileTable);
                Assert.True(slot.FileTable.Count > 0, $"{seasonKey}: FileTable should not be empty");

                // Verify Metadata and Choices exist
                if (slot.Metadata == null)
                {
                    failures.Add($"NO METADATA: {seasonKey}");
                    continue;
                }
                if (slot.Choices == null)
                {
                    failures.Add($"NO CHOICES: {seasonKey}");
                    continue;
                }

                log.Add($"  {seasonKey}: created OK, FileTable={slot.FileTable.Count} entries");
                log.Add($"    Metadata props: {slot.Metadata.AllProperties.Count()}");
                log.Add($"    Choices groups: {slot.Choices.TypeGroups.Count}");

                // Write and read back
                var writtenBytes = BundleWriter.Write(slot);
                Assert.True(writtenBytes.Length > 0, $"{seasonKey}: Written bytes should not be empty");

                var reloaded = BundleReader.Read(writtenBytes, fileName);
                Assert.NotNull(reloaded.Metadata);
                Assert.NotNull(reloaded.Choices);

                // Verify properties survive round-trip
                var origMetaCount = slot.Metadata.AllProperties.Count();
                var reloadedMetaCount = reloaded.Metadata.AllProperties.Count();
                if (origMetaCount != reloadedMetaCount)
                {
                    failures.Add($"ROUND-TRIP META COUNT MISMATCH: {seasonKey} orig={origMetaCount} reloaded={reloadedMetaCount}");
                }

                // For S1/S2: verify choice count matches expected from ChoiceDatabase
                if (seasonKey is "s1" or "s1_400days" or "s2")
                {
                    var dbChoices = ChoiceDatabase.ForEpisode(seasonKey, 1).ToList();
                    var accessor = new SaveAccessor(reloaded.Choices);
                    var allChoices = accessor.GetAllChoices();

                    log.Add($"    DB choices for ep1: {dbChoices.Count}, save choices: {allChoices.Count}");

                    if (allChoices.Count != dbChoices.Count)
                    {
                        failures.Add($"CHOICE COUNT MISMATCH: {seasonKey} db={dbChoices.Count} save={allChoices.Count}");
                    }
                }

                log.Add($"    Round-trip: {writtenBytes.Length} bytes, reload OK");
                log.Add("");
            }
            catch (Exception ex)
            {
                failures.Add($"ERROR: {seasonKey} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics
        Assert.Fail(string.Join(Environment.NewLine, log));
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 6: ChoiceDatabase completeness
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChoiceDatabaseIsComplete()
    {
        var log = new List<string>();
        var failures = new List<string>();

        var seasonKeys = new[] { "s1", "s1_400days", "s2", "michonne", "s3", "s4" };

        log.Add("ChoiceDatabase counts per season:");
        foreach (var seasonKey in seasonKeys)
        {
            var choices = ChoiceDatabase.ForSeason(seasonKey).ToList();

            if (choices.Count == 0)
            {
                failures.Add($"NO CHOICES DEFINED for season: {seasonKey}");
                continue;
            }

            log.Add($"  {seasonKey}: {choices.Count} choices");
        }

        log.Add("");
        log.Add($"Total choices: {ChoiceDatabase.AllChoices.Count}");
        log.Add("");

        // Verify all S1 choice keys are non-empty strings
        var s1Choices = ChoiceDatabase.ForSeason("s1").ToList();
        log.Add("S1 choice keys:");
        foreach (var choice in s1Choices)
        {
            if (string.IsNullOrWhiteSpace(choice.ChoiceKey))
            {
                failures.Add($"EMPTY CHOICE KEY: {choice.Description}");
            }
            log.Add($"  ep{choice.Episode}: \"{choice.ChoiceKey}\" ({choice.Description})");
        }

        if (failures.Count > 0)
        {
            log.Add("");
            log.Add("--- FAILURES ---");
            log.AddRange(failures);
            Assert.Fail(string.Join(Environment.NewLine, log));
        }

        // Dump diagnostics
        Assert.Fail(string.Join(Environment.NewLine, log));
    }
}
