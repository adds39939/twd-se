using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Core.Tests.Integration;

/// <summary>
/// Integration tests that parse real sample .bundle save files from TestData.
/// </summary>
public class BundleIntegrationTests
{
    [Fact]
    public void BundleReader_ParsesRealSaveFile()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
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
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
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
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
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
}
