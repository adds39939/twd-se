using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Integration tests that parse real sample .bundle save files.
/// These tests pass (as no-op) if sample files are not present.
/// </summary>
public class BundleIntegrationTests
{
    private static readonly string SampleDir =
        @"C:\Users\Adam\Downloads\pc-the-walking-dead-the-telltale-definitive-series-savegame";

    private static readonly string[] SampleFiles =
    [
        @"Season 1\Episodes 1,2,3\wd1_saveslot1.bundle",
        @"Season 1\Episodes 1,2,3\wd1_saveslot2.bundle",
        @"Season 1\Episodes 1,2,3\wd1_saveslot3.bundle",
        @"Season 1\Episodes 4,5, Episode 400\wd1_saveslot1.bundle",
        @"Season 1\Episodes 4,5, Episode 400\wd1_saveslot2.bundle",
        @"Season 1\Episodes 4,5, Episode 400\wd1_saveslot3.bundle",
    ];

    private static string? FindSampleFile()
    {
        foreach (var rel in SampleFiles)
        {
            var path = Path.Combine(SampleDir, rel);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    [Fact]
    public void BundleReader_ParsesRealSaveFile()
    {
        var path = FindSampleFile();
        if (path == null) return; // Skip if no sample files

        var slot = BundleReader.Read(path);

        Assert.NotNull(slot);
        Assert.NotEmpty(slot.FileTable);
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.Choices);
        Assert.True(slot.Choices.TypeGroups.Count > 0, "Choices should have type groups");
    }

    [Fact]
    public void SaveAccessor_ReadsChoicesFromRealSave()
    {
        var path = FindSampleFile();
        if (path == null) return;

        var slot = BundleReader.Read(path);
        var accessor = new SaveAccessor(slot.Choices!, slot.Metadata);

        var allChoices = accessor.GetAllChoices();
        Assert.NotEmpty(allChoices);

        // Should find at least one known S1 choice key
        var knownKeys = new[] { "dougcarley_saved", "lied_to_hershel", "shawnduck_choice" };
        Assert.True(allChoices.Any(c => knownKeys.Contains(c.key)),
            $"Expected at least one known choice key. Found: {string.Join(", ", allChoices.Select(c => c.key).Take(5))}");
    }

    [Fact]
    public void BundleWriter_RoundTripsWithoutCrash()
    {
        var path = FindSampleFile();
        if (path == null) return;

        var slot = BundleReader.Read(path);

        // Write it back
        var output = BundleWriter.Write(slot);
        Assert.NotNull(output);
        Assert.True(output.Length > 0);

        // Re-parse the written output
        var reparsed = BundleReader.Read(output, "test.bundle");
        Assert.NotNull(reparsed.Metadata);
        Assert.NotNull(reparsed.Choices);

        // Verify choices survive round-trip
        var origAccessor = new SaveAccessor(slot.Choices!, slot.Metadata);
        var newAccessor = new SaveAccessor(reparsed.Choices!, reparsed.Metadata);

        var origChoices = origAccessor.GetAllChoices();
        var newChoices = newAccessor.GetAllChoices();
        Assert.Equal(origChoices.Count, newChoices.Count);
    }

    [Fact]
    public void AllSampleFiles_ParseSuccessfully()
    {
        if (!Directory.Exists(SampleDir)) return;

        var failures = new List<string>();
        foreach (var rel in SampleFiles)
        {
            var path = Path.Combine(SampleDir, rel);
            if (!File.Exists(path)) continue;

            try
            {
                var slot = BundleReader.Read(path);
                Assert.NotNull(slot.Metadata);
                Assert.NotNull(slot.Choices);

                var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
                var choices = accessor.GetAllChoices();
                Assert.NotEmpty(choices);
            }
            catch (Exception ex)
            {
                failures.Add($"{rel}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0,
            $"Failed to parse {failures.Count} file(s):\n{string.Join("\n", failures)}");
    }
}
