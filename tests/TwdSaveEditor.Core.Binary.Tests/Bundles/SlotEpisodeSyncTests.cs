using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Binary.Bundles;

namespace TwdSaveEditor.Core.Binary.Tests.Bundles;

public class SlotEpisodeSyncTest
{
    private const ulong SlotEpisodeIdHash = 0xB218E7C003A67CE9;
    private const ulong ProgressHash = 0x94C245DACB1ADDC3;
    private const ulong LastEpFinishedHash = 0x0399C2FFE0D50348;
    private const ulong EpisodesCompletedHash = 0xFD50E3BE7B29A8B1;

    [Fact]
    public void SyncEpisode_SetsAllRequiredProperties()
    {
        var slotPath = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(slotPath))
        {
            return;
        }

        var slotData = File.ReadAllBytes(slotPath);
        var slotSave = BundleReader.Read(slotData, "wd1_saveslot1.bundle");

        SetSlotProperty(slotSave.Metadata!, SlotEpisodeIdHash,
            new StringValue("WalkingDead103"), "String");
        SetSlotProperty(slotSave.Metadata!, ProgressHash,
            new IntValue(3), "int32");
        SetSlotProperty(slotSave.Metadata!, LastEpFinishedHash,
            new IntValue(2), "int32");
        SetSlotProperty(slotSave.Metadata!, EpisodesCompletedHash,
            new IntValue(2), "int32");

        var written = BundleWriter.Write(slotSave);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

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
        var slotPath = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        if (!File.Exists(slotPath))
        {
            return;
        }

        var slotData = File.ReadAllBytes(slotPath);
        var slotSave = BundleReader.Read(slotData, "wd1_saveslot2.bundle");

        var origEp = slotSave.Metadata!.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.NotNull(origEp);

        SetSlotProperty(slotSave.Metadata!, SlotEpisodeIdHash,
            new StringValue("WalkingDead105"), "String");
        SetSlotProperty(slotSave.Metadata!, ProgressHash,
            new IntValue(5), "int32");

        var afterEp = slotSave.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == SlotEpisodeIdHash);
        Assert.Equal("WalkingDead105", ((StringValue)afterEp.Value).Value);

        var afterProgress = slotSave.Metadata.AllProperties
            .First(p => p.KeySymbol.Value == ProgressHash);
        Assert.Equal(5, ((IntValue)afterProgress.Value).Value);

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
                {
                    existingSv.Value = sv.Value;
                }
                else if (value is IntValue iv && prop.Value is IntValue existingIv)
                {
                    existingIv.Value = iv.Value;
                }

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
