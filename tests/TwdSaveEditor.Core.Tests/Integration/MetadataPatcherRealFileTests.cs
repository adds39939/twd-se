using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

public class MetadataPatcherRealFileTests
{
    [Fact]
    public void PatchRealAutosave_CanBeReloaded()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path))
        {
            return;
        }

        var originalBytes = File.ReadAllBytes(path);

        var slot = BundleReader.Read(originalBytes, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.RawMetadataFile);

        var epProp = slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);

        if (epProp?.Value is StringValue sv)
        {
            var original = sv.Value;
            sv.Value = "WalkingDead102";

            var patched = MetadataPatcher.PatchMetadata(originalBytes, slot.Metadata, slot.RawMetadataFile);

            var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
            Assert.NotNull(reloaded.Metadata);

            var reloadedEp = reloaded.Metadata.AllProperties
                .FirstOrDefault(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);
            Assert.NotNull(reloadedEp);
            Assert.Equal("WalkingDead102", ((StringValue)reloadedEp.Value).Value);
        }
        else
        {
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

        var patched = MetadataPatcher.PatchMetadata(originalBytes, slot.Metadata, slot.RawMetadataFile);

        var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(reloaded.Metadata);

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

        var defaultSave = slot.RawInnerFiles?.GetValueOrDefault("default.save");
        Assert.NotNull(defaultSave);
        Assert.True(defaultSave.Length > 0, "default.save should have data");

        var patched = MetadataPatcher.PatchMetadata(
            originalBytes, slot.Metadata, slot.RawMetadataFile, clearDefaultSave: true);

        var reloaded = BundleReader.Read(patched, "_wd1_saveslot1_autosave.bundle");
        var reloadedDefaultSave = reloaded.RawInnerFiles?.GetValueOrDefault("default.save");
        Assert.NotNull(reloadedDefaultSave);

        var nonZeroCount = reloadedDefaultSave.Count(b => b != 0);
        if (nonZeroCount > 0)
        {
            var firstNonZero = Array.FindIndex(reloadedDefaultSave, b => b != 0);
            var snippet = BitConverter.ToString(reloadedDefaultSave, firstNonZero, Math.Min(32, reloadedDefaultSave.Length - firstNonZero));
            Assert.Fail(
                $"default.save should be all zeros after clearDefaultSave, but has {nonZeroCount} non-zero bytes. " +
                $"Total size={reloadedDefaultSave.Length}, first non-zero at offset {firstNonZero}: {snippet}");
        }
    }
}
