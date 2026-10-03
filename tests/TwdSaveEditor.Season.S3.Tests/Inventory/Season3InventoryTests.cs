using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Season.S3.Saves;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S3.Tests.Inventory;

public class Season3InventoryTests
{
    private const string Crowbar = "Inventory - Crowbar";
    private const string Siphon = "Inventory - Siphon";
    private const string Batteries = "Inventory - Batteries";
    private const string CandyBar = "Inventory - Candy Bar";
    private const string Water = "Inventory - Bottle Water";

    [Fact]
    public void RealSave_ListsWhatJavierHoldsAndTheItemsOfItsEpisode()
    {
        var state = Season3Saves.Handler.GetInventory(Season3Saves.LoadEpisode1Save());

        Assert.True(state.Editable);
        Assert.False(state.CarriedItemsKnown);
        Assert.Equal("Javier", state.Owner);
        Assert.Equal(1, state.Episode);
        Assert.Equal([CandyBar], Ids(state.Held));
        Assert.Equal([Crowbar, Siphon, Batteries, CandyBar], state.Items.Select(item => item.Id));
        Assert.Equal("Candy Bar", state.Items.Single(item => item.Id == CandyBar).Name);
    }

    [Fact]
    public void ChangingTheItemsOfARealSave_WritesBothTheSharedAndJaviersInventory()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var before = Assert.Single(slot.Checkpoints).Files.ToDictionary(file => file.NameSymbol, file => file.Data.ToArray());

        Season3Saves.Handler.SetInventory(slot, Held(Crowbar, Batteries));

        var reloaded = Season3Saves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        Assert.Equal([Crowbar, Batteries], Ids(Season3Saves.Handler.GetInventory(reloaded).Held));
        foreach (var name in new[] { S3SlotFiles.InventoryProperties, S3SlotFiles.OwnerInventoryProperties })
        {
            var inventory = Runtime(save, name);
            Assert.Equal(1, inventory.GetInt(Crowbar));
            Assert.Equal(1, inventory.GetInt(Batteries));
            Assert.Equal(0, inventory.GetInt(Siphon));
            Assert.Equal(0, inventory.GetInt(CandyBar));
        }

        var changed = save.Files.Where(file => !before[file.NameSymbol].AsSpan().SequenceEqual(file.Data)).Select(file => file.NameSymbol);
        Assert.Equal(new[] { S3SlotFiles.InventoryProperties, S3SlotFiles.OwnerInventoryProperties }.Order(), changed.Order());
    }

    [Fact]
    public void UntouchedRealInventory_IsWrittenBackByteForByte()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var before = BundleWriter.Write(Assert.Single(slot.Checkpoints));

        Season3Saves.Handler.SetInventory(slot, Held(CandyBar));

        Assert.Equal(before, BundleWriter.Write(Assert.Single(slot.Checkpoints)));
    }

    [Theory]
    [InlineData(1, "Junkyard", new string[0])]
    [InlineData(1, "JunkyardHill", new[] { Crowbar, Siphon })]
    [InlineData(1, "VirginiaRoadTruck", new[] { CandyBar })]
    [InlineData(1, "AirportGate", new string[0])]
    [InlineData(2, "VirginiaRoadCar", new[] { Water })]
    [InlineData(2, "VirginiaUnderpass", new string[0])]
    public void ChapterSave_IsOfferedTheItemsPickedUpEarlierInTheEpisode(int episode, string chapter, string[] expected)
    {
        var slot = Season3Saves.LoadEpisode1Save();
        Season3Saves.Handler.RestartFromChapter(slot, episode, chapter);

        var state = Season3Saves.Handler.GetInventory(slot);

        Assert.True(state.Editable);
        Assert.True(state.CarriedItemsKnown);
        Assert.Equal(episode, state.Episode);
        Assert.Equal(4, state.Items.Count);
        Assert.Empty(state.Held);
        Assert.Equal(expected, Ids(Season3Saves.Handler.GetCarriedItems(slot)));
    }

    [Fact]
    public void ItemsGivenToAChapterSave_AreRegisteredInItsSaveGame()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        Season3Saves.Handler.RestartFromChapter(slot, 1, "JunkyardHill");

        Season3Saves.Handler.SetInventory(slot, Season3Saves.Handler.GetCarriedItems(slot));

        var reloaded = Season3Saves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var game = SaveGameCodec.Read(save.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal(save.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Contains(S3SlotFiles.InventoryProperties, game.RuntimePropertyNames);
        Assert.Contains(S3SlotFiles.OwnerInventoryProperties, game.RuntimePropertyNames);
        Assert.Equal([Crowbar, Siphon], Ids(Season3Saves.Handler.GetInventory(reloaded).Held));
        Assert.Equal(1, Runtime(save, S3SlotFiles.OwnerInventoryProperties).GetInt(Siphon));
        Assert.Null(Runtime(save, S3SlotFiles.OwnerInventoryProperties).Find(Batteries));
        Assert.Equal("Junkyard Hill", Season3Saves.Handler.GetResumeState(reloaded).Checkpoint);
    }

    [Fact]
    public void EpisodeWithoutItems_HasNoInventoryToEdit()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        Season3Saves.Handler.RestartFromChapter(slot, 3, "RichmondChurch");

        var state = Season3Saves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.Contains("Episodes 1 and 2", state.Unavailable);
        Assert.Empty(Season3Saves.Handler.GetCarriedItems(slot));
    }

    [Fact]
    public void SaveThatStartsAnEpisode_HasNoInventoryToEdit()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        Season3Saves.Handler.RestartFromEpisode(slot, 2);

        var state = Season3Saves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.Contains("Resume Point", state.Unavailable);
        Assert.Throws<InvalidOperationException>(() => Season3Saves.Handler.SetInventory(slot, Held(Water)));
    }

    private static List<string> Ids(IEnumerable<HeldItem> items) => [.. items.Select(item => item.Id)];

    private static List<HeldItem> Held(params string[] ids) => [.. ids.Select(id => new HeldItem(id))];

    private static PropertySet Runtime(SaveSlot save, ulong name)
    {
        var file = save.FindFile(name);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
