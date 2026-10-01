using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

public class FullResumePointEditTest
{
    private const ulong SlotEpisodeIdHash = 0xB218E7C003A67CE9;
    private const ulong ProgressHash = 0x94C245DACB1ADDC3;

    [Fact]
    public void ChangeEpisode_UpdatesBothSlotAndAutosave()
    {
        var slotPath = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        var autoPath = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(slotPath) || !File.Exists(autoPath)) return;

        var slotData = File.ReadAllBytes(slotPath);
        var autoData = File.ReadAllBytes(autoPath);
        var slotSave = BundleReader.Read(slotData, "wd1_saveslot1.bundle");
        var autoSave = BundleReader.Read(autoData, "_wd1_saveslot1_autosave.bundle");

        var origAutoEp = autoSave.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);
        Assert.Equal("WalkingDead101", ((StringValue)origAutoEp.Value).Value);

        ((StringValue)origAutoEp.Value).Value = "WalkingDead102";

        var patchedAuto = MetadataPatcher.PatchMetadata(
            autoSave.RawBundleData!, autoSave.Metadata, autoSave.RawMetadataFile!);

        SetProperty(slotSave.Metadata!, SlotEpisodeIdHash,
            new StringValue("WalkingDead102"), "String");
        SetProperty(slotSave.Metadata!, ProgressHash,
            new IntValue(2), "int32");

        var patchedSlot = BundleWriter.Write(slotSave);

        var reloadedAuto = BundleReader.Read(patchedAuto, "_wd1_saveslot1_autosave.bundle");
        var reloadedAutoEp = reloadedAuto.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);
        Assert.Equal("WalkingDead102", ((StringValue)reloadedAutoEp.Value).Value);

        var reloadedCheckpoint = reloadedAuto.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == AutosaveHashes.CheckpointDialog);
        Assert.NotEqual("", ((StringValue)reloadedCheckpoint.Value).Value);

        var reloadedSlot = BundleReader.Read(patchedSlot, "wd1_saveslot1.bundle");
        var reloadedSlotEp = reloadedSlot.Metadata!.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.NotNull(reloadedSlotEp);
        Assert.Equal("WalkingDead102", ((StringValue)reloadedSlotEp.Value).Value);

        var reloadedProgress = reloadedSlot.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == ProgressHash);
        Assert.Equal(2, ((IntValue)reloadedProgress.Value).Value);

        var tempDir = TestDataHelper.CreateTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "slot.bundle"), patchedSlot);
            File.WriteAllBytes(Path.Combine(tempDir, "auto.bundle"), patchedAuto);

            var diskSlot = BundleReader.Read(
                File.ReadAllBytes(Path.Combine(tempDir, "slot.bundle")), "slot.bundle");
            var diskAuto = BundleReader.Read(
                File.ReadAllBytes(Path.Combine(tempDir, "auto.bundle")), "auto.bundle");

            Assert.Equal("WalkingDead102",
                ((StringValue)diskSlot.Metadata!.AllProperties
                    .First(p => p.KeySymbol.Value == SlotEpisodeIdHash).Value).Value);
            Assert.Equal(2,
                ((IntValue)diskSlot.Metadata.AllProperties
                    .First(p => p.KeySymbol.Value == ProgressHash).Value).Value);
            Assert.Equal("WalkingDead102",
                ((StringValue)diskAuto.Metadata!.AllProperties
                    .First(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId).Value).Value);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static void SetProperty(PropertySet metadata, ulong hash, PropertyValue value, string typeName)
    {
        foreach (var group in metadata.TypeGroups)
        {
            var prop = group.Properties.FirstOrDefault(p => p.KeySymbol.Value == hash);
            if (prop != null)
            {
                if (value is StringValue sv && prop.Value is StringValue existingSv)
                    existingSv.Value = sv.Value;
                else if (value is IntValue iv && prop.Value is IntValue existingIv)
                    existingIv.Value = iv.Value;
                return;
            }
        }

        var typeSymbol = Symbol.FromString(typeName);
        var targetGroup = metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == typeSymbol);
        if (targetGroup == null)
        {
            targetGroup = new TypeGroup(typeSymbol);
            metadata.TypeGroups.Add(targetGroup);
        }
        targetGroup.Properties.Add(new Property(new Symbol(hash), value));
    }
}
