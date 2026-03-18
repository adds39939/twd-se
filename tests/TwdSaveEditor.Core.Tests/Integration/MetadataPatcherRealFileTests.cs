using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

/// <summary>
/// Tests MetadataPatcher against a real autosave bundle from TestData.
/// </summary>
public class MetadataPatcherRealFileTests
{
    [Fact]
    public void PatchRealAutosave_CanBeReloaded()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path))
        {
            // Skip if test data not available
            return;
        }

        var originalBytes = File.ReadAllBytes(path);

        // Parse the autosave
        var slot = BundleReader.Read(originalBytes, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.RawMetadataFile);

        // Find and modify the episode ID string
        var epProp = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == ResumePoint.AutosaveHashes.EpisodeId);

        if (epProp?.Value is StringValue sv)
        {
            var original = sv.Value;
            sv.Value = "WalkingDead102";

            // Patch
            var patched = MetadataPatcher.PatchMetadata(originalBytes, slot.Metadata, slot.RawMetadataFile);

            // Reload the patched bundle — this is where the bug would surface
            var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
            Assert.NotNull(reloaded.Metadata);

            var reloadedEp = reloaded.Metadata.AllProperties
                .FirstOrDefault(p => p.KeySymbol.Value == ResumePoint.AutosaveHashes.EpisodeId);
            Assert.NotNull(reloadedEp);
            Assert.Equal("WalkingDead102", ((StringValue)reloadedEp.Value).Value);
        }
        else
        {
            // If no episode ID, just test that patching doesn't corrupt
            var patched = MetadataPatcher.PatchMetadata(originalBytes, slot.Metadata, slot.RawMetadataFile);
            var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
            Assert.NotNull(reloaded.Metadata);
        }
    }

    [Fact]
    public void PatchRealAutosave_ProducesValidBundle_WhenUnchanged()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path))
            return;

        var originalBytes = File.ReadAllBytes(path);
        var slot = BundleReader.Read(originalBytes, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.RawMetadataFile);

        // Patch without changing anything — file may be larger (TTCZ decompressed to raw)
        // but should still be a valid loadable bundle
        var patched = MetadataPatcher.PatchMetadata(originalBytes, slot.Metadata, slot.RawMetadataFile);

        var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(reloaded.Metadata);

        // All original properties should be preserved
        var origPropCount = slot.Metadata.AllProperties.Count();
        var reloadedPropCount = reloaded.Metadata.AllProperties.Count();
        Assert.Equal(origPropCount, reloadedPropCount);
    }

    [Fact]
    public void PatchRealAutosave_ClearDefaultSave_ZeroesData()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path)) return;

        var originalBytes = File.ReadAllBytes(path);
        var slot = BundleReader.Read(originalBytes, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.RawMetadataFile);

        // Verify default.save exists in the original
        var defaultSave = slot.RawInnerFiles?.GetValueOrDefault("default.save");
        Assert.NotNull(defaultSave);
        Assert.True(defaultSave.Length > 0, "default.save should have data");

        // Patch with clearDefaultSave=true
        var patched = MetadataPatcher.PatchMetadata(
            originalBytes, slot.Metadata, slot.RawMetadataFile, clearDefaultSave: true);

        // Reload and check default.save is zeroed
        var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
        var reloadedDefaultSave = reloaded.RawInnerFiles?.GetValueOrDefault("default.save");
        Assert.NotNull(reloadedDefaultSave);

        // All bytes should be zero
        var nonZeroCount = reloadedDefaultSave.Count(b => b != 0);
        if (nonZeroCount > 0)
        {
            // Find first non-zero byte position
            var firstNonZero = Array.FindIndex(reloadedDefaultSave, b => b != 0);
            var snippet = BitConverter.ToString(reloadedDefaultSave, firstNonZero, Math.Min(32, reloadedDefaultSave.Length - firstNonZero));
            Assert.Fail(
                $"default.save should be all zeros after clearDefaultSave, but has {nonZeroCount} non-zero bytes. " +
                $"Total size={reloadedDefaultSave.Length}, first non-zero at offset {firstNonZero}: {snippet}");
        }
    }
}
