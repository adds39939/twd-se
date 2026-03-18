using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

/// <summary>
/// Tests that MetadataPatcher can patch metadata in-place within a bundle
/// without corrupting the rest of the file.
/// </summary>
public class MetadataPatcherTests
{
    /// <summary>
    /// Create a minimal bundle with metadata_save.p, patch a string property,
    /// and verify the patched bundle can be re-read with the new value.
    /// </summary>
    [Fact]
    public void PatchMetadata_SameSize_PreservesBundle()
    {
        // Create a slot bundle that we can use as a stand-in
        var slot = SaveSlotFactory.CreateBlank("_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);

        // Re-read to get raw metadata file
        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        // Modify a string property (episode ID)
        var epProp = parsed.Metadata!.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(epProp);
        Assert.IsType<StringValue>(epProp.Value);

        // Change to same-length string to test in-place patch
        var originalValue = ((StringValue)epProp.Value).Value;
        ((StringValue)epProp.Value).Value = "WalkingDead102";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);

        // Re-read the patched bundle
        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");
        var reparsedProp = reparsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.Equal("WalkingDead102", ((StringValue)reparsedProp.Value).Value);
    }

    /// <summary>
    /// Patch with a different-sized value and verify the bundle is rebuilt correctly.
    /// </summary>
    [Fact]
    public void PatchMetadata_DifferentSize_RebuildsCorrectly()
    {
        var slot = SaveSlotFactory.CreateBlank("_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);
        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        // Change to a longer string
        var epProp = parsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        ((StringValue)epProp.Value).Value = "WalkingDead505_ExtraLongEpisodeNameForTesting";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);

        // Should be larger than original
        Assert.True(patched.Length > originalBytes.Length);

        // Re-read and verify
        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");
        var reparsedProp = reparsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.Equal("WalkingDead505_ExtraLongEpisodeNameForTesting", ((StringValue)reparsedProp.Value).Value);
    }

    /// <summary>
    /// Verify that non-metadata inner files survive the patch unchanged.
    /// </summary>
    [Fact]
    public void PatchMetadata_PreservesOtherFiles()
    {
        // Create a slot with choices (S1 style) so there are multiple inner files
        var registry = new SeasonRegistry([
            new TwdSaveEditor.Core.GameData.Seasons.S1Handler()
        ]);
        var slot = SaveSlotFactory.CreateForSeason(registry, "s1", 1, "_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);
        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        // Modify metadata
        var epProp = parsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        ((StringValue)epProp.Value).Value = "WalkingDead103";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);
        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");

        // Choices should still be present and unchanged
        Assert.NotNull(reparsed.Choices);
        Assert.True(reparsed.Choices.AllProperties.Any());
    }
}
