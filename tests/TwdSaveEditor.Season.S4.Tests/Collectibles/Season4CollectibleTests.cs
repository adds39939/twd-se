using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S4.Tests.Collectibles;

public class Season4CollectibleTests
{
    private const string SkullCatFound = "Collectible Found - Skull Cat";
    private const string SkullCatPlaced = "Collectible Placed - Skull Cat";
    private const string CrystalFound = "Collectible Found - Crystal";

    [Fact]
    public void RealSave_ListsTheCollectiblesOfEveryEpisodeAndWhichWereFound()
    {
        var state = Season4Saves.Handler.GetInventory(Season4Saves.LoadEpisode1Save());

        Assert.True(state.Editable);
        Assert.False(state.CarriedItemsKnown);
        Assert.Equal("Clementine", state.Owner);
        Assert.Equal(40, state.Items.Count);
        Assert.Equal("Skull Cat (Episode 1)", state.Items.Single(item => item.Id == SkullCatFound).Name);
        Assert.Equal("Crystal (placed) (Episode 4)", state.Items.Single(item => item.Id == "Collectible Placed - Crystal").Name);
        Assert.NotEmpty(state.Held);
        Assert.All(state.Held, held => Assert.StartsWith("Collectible ", held.Id));
        Assert.DoesNotContain(CrystalFound, state.Held.Select(held => held.Id));
    }

    [Fact]
    public void ChangedCollectibles_AreWrittenToTheSlotAndReadBack()
    {
        var slot = Season4Saves.LoadEpisode1Save();
        var before = Season4Saves.Handler.GetInventory(slot).Held.Select(held => held.Id).ToList();
        var wanted = before.Where(id => id != SkullCatFound && id != SkullCatPlaced).Append(CrystalFound).Select(id => new HeldItem(id)).ToList();

        Season4Saves.Handler.SetInventory(slot, wanted);

        var reloaded = BundleReader.Read(BundleWriter.Write(slot), slot.FileName);
        reloaded.DetectedSeasonKey = "s4";
        var held = Season4Saves.Handler.GetInventory(reloaded).Held.Select(item => item.Id).ToList();
        Assert.Contains(CrystalFound, held);
        Assert.DoesNotContain(SkullCatFound, held);
        Assert.DoesNotContain(SkullCatPlaced, held);
        Assert.Null(reloaded.Metadata!.Find(SkullCatFound));
        Assert.True(reloaded.Metadata.GetBool(CrystalFound));
        Assert.Equal(wanted.Count, held.Count);
    }

    [Fact]
    public void UntouchedCollectibles_LeaveTheSlotByteForByte()
    {
        var slot = Season4Saves.LoadEpisode1Save();
        var before = BundleWriter.Write(slot);

        Season4Saves.Handler.SetInventory(slot, Season4Saves.Handler.GetInventory(slot).Held);

        Assert.Equal(before, BundleWriter.Write(slot));
    }
}
