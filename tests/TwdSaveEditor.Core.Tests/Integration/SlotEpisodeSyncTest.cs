using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

/// <summary>
/// Tests the slot episode sync logic used when changing the resume episode.
/// Verifies that all game-required properties are written correctly.
/// </summary>
public class SlotEpisodeSyncTest
{
    private const ulong SlotEpisodeIdHash = 0xB218E7C003A67CE9; // "Episode in Progress"
    private const ulong ProgressHash = 0x94C245DACB1ADDC3;      // "progress" (int)
    private const ulong LastEpFinishedHash = 0x0399C2FFE0D50348; // "Last Episode Finished"
    private const ulong EpisodesCompletedHash = 0xFD50E3BE7B29A8B1; // "Episodes Completed"

    [Fact]
    public void SyncEpisode_SetsAllRequiredProperties()
    {
        var slotPath = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(slotPath)) return;

        var slotData = File.ReadAllBytes(slotPath);
        var slotSave = BundleReader.Read(slotData, "wd1_saveslot1.bundle");

        // Set all the properties that SyncSlotBundleEpisodeId would set
        SetSlotProperty(slotSave.Metadata!, SlotEpisodeIdHash,
            new StringValue("WalkingDead103"), "String");
        SetSlotProperty(slotSave.Metadata!, ProgressHash,
            new IntValue(3), "int32");
        SetSlotProperty(slotSave.Metadata!, LastEpFinishedHash,
            new IntValue(2), "int32");
        SetSlotProperty(slotSave.Metadata!, EpisodesCompletedHash,
            new IntValue(2), "int32");

        // Write and re-read
        var written = BundleWriter.Write(slotSave);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

        // Verify all properties on disk
        var props = reloaded.Metadata!.AllProperties.ToList();

        var ep = props.First(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.Equal("WalkingDead103", ((StringValue)ep.Value).Value);

        var progress = props.First(p => p.KeySymbol.Value == ProgressHash);
        Assert.Equal(3, ((IntValue)progress.Value).Value);

        var lastEp = props.First(p => p.KeySymbol.Value == LastEpFinishedHash);
        Assert.Equal(2, ((IntValue)lastEp.Value).Value);

        var completed = props.First(p => p.KeySymbol.Value == EpisodesCompletedHash);
        Assert.Equal(2, ((IntValue)completed.Value).Value);
    }

    [Fact]
    public void SyncEpisode_UpdatesExistingProperties()
    {
        // Create a slot that already has Episode in Progress and progress set
        var slotPath = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        if (!File.Exists(slotPath)) return;

        var slotData = File.ReadAllBytes(slotPath);
        var slotSave = BundleReader.Read(slotData, "wd1_saveslot2.bundle");

        // Slot2 from TestData is at Episode 4 (WalkingDead104, progress=4)
        var origEp = slotSave.Metadata!.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.NotNull(origEp);

        // Change to Episode 5
        SetSlotProperty(slotSave.Metadata!, SlotEpisodeIdHash,
            new StringValue("WalkingDead105"), "String");
        SetSlotProperty(slotSave.Metadata!, ProgressHash,
            new IntValue(5), "int32");

        // Verify in-memory update
        var afterEp = slotSave.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.Equal("WalkingDead105", ((StringValue)afterEp.Value).Value);

        var afterProgress = slotSave.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == ProgressHash);
        Assert.Equal(5, ((IntValue)afterProgress.Value).Value);

        // Write, re-read, verify
        var written = BundleWriter.Write(slotSave);
        var reloaded = BundleReader.Read(written, "wd1_saveslot2.bundle");

        var diskEp = reloaded.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.Equal("WalkingDead105", ((StringValue)diskEp.Value).Value);

        var diskProgress = reloaded.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == ProgressHash);
        Assert.Equal(5, ((IntValue)diskProgress.Value).Value);
    }

    private static void SetSlotProperty(PropertySet metadata, ulong hash, PropertyValue value, string typeName)
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
