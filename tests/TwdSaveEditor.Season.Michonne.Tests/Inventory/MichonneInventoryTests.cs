using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Saves;

namespace TwdSaveEditor.Season.Michonne.Tests.Inventory;

public class MichonneInventoryTests
{
    private const string Machete = "Inventory - Machete";
    private const string Binoculars = "Inventory - Binoculars";
    private const string Flashlight = "Inventory - Flashlight";
    private const string Screwdriver = "Inventory - Screwdriver";
    private const string Rebar = "Inventory - Rebar";

    [Fact]
    public void RealSave_ListsTheItemsOfItsEpisode()
    {
        var state = MichonneSaves.Handler.GetInventory(MichonneSaves.LoadEpisode1Save());

        Assert.True(state.Editable);
        Assert.False(state.CarriedItemsKnown);
        Assert.Equal("Michonne", state.Owner);
        Assert.Equal(1, state.Episode);
        Assert.Equal([Machete, Binoculars, Flashlight, Screwdriver, Rebar], state.Items.Select(item => item.Id));
        Assert.DoesNotContain(Machete, Ids(state.Held));
    }

    [Fact]
    public void ChangingTheItemsOfARealSave_RewritesOnlyItsInventory()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        var save = slot.Checkpoints.Single(candidate => candidate.FileName == MichonneSaves.Autosave);
        var before = save.Files.ToDictionary(file => file.NameSymbol, file => file.Data.ToArray());

        MichonneSaves.Handler.SetInventory(slot, Held(Machete, Rebar));

        var reloaded = MichonneSaves.Reload(slot);
        var written = reloaded.Checkpoints.Single(candidate => candidate.FileName == MichonneSaves.Autosave);
        Assert.Equal([Machete, Rebar], Ids(MichonneSaves.Handler.GetInventory(reloaded).Held));
        var inventory = Runtime(written, StoryFiles.InventoryProperties);
        Assert.Equal(1, inventory.GetInt(Machete));
        Assert.Equal(1, inventory.GetInt(Rebar));
        Assert.NotEqual(1, inventory.GetInt(Screwdriver));

        var changed = written.Files.Where(file => !before[file.NameSymbol].AsSpan().SequenceEqual(file.Data)).Select(file => file.NameSymbol);
        Assert.Equal([StoryFiles.InventoryProperties], changed);
    }

    [Theory]
    [InlineData("DockCBayPeteBoatTopDeck", new string[0])]
    [InlineData("FerryExteriorDeck", new[] { Machete, Binoculars, Flashlight })]
    [InlineData("FerryInteriorSnackBar", new[] { Machete, Binoculars, Flashlight })]
    [InlineData("Monroe", new string[0])]
    [InlineData("FlagShipInteriorEscape", new string[0])]
    [InlineData("NextTimeOn", new[] { Screwdriver })]
    public void ChapterCheckpoint_IsOfferedTheItemsPickedUpEarlierInTheEpisode(string chapter, string[] expected)
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        MichonneSaves.Handler.RestartFromChapter(slot, 1, chapter);

        var state = MichonneSaves.Handler.GetInventory(slot);

        Assert.True(state.Editable);
        Assert.True(state.CarriedItemsKnown);
        Assert.Empty(state.Held);
        Assert.Equal(expected, Ids(MichonneSaves.Handler.GetCarriedItems(slot)));
    }

    [Fact]
    public void ItemsGivenToAChapterCheckpoint_AreRegisteredInItsSaveGame()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        MichonneSaves.Handler.RestartFromChapter(slot, 1, "FerryExteriorDeck");

        MichonneSaves.Handler.SetInventory(slot, MichonneSaves.Handler.GetCarriedItems(slot));

        var reloaded = MichonneSaves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var game = SaveGameCodec.Read(save.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal(save.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Contains(StoryFiles.InventoryProperties, game.RuntimePropertyNames);
        Assert.Equal([Machete, Binoculars, Flashlight], Ids(MichonneSaves.Handler.GetInventory(reloaded).Held));
        Assert.Equal(1, Runtime(save, StoryFiles.InventoryProperties).GetInt(Flashlight));
        Assert.Equal("Ferry Exterior Deck", MichonneSaves.Handler.GetResumeState(reloaded).Checkpoint);
    }

    [Fact]
    public void EpisodeWithoutItems_HasNoInventoryToEdit()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        MichonneSaves.Handler.RestartFromChapter(slot, 2, "WoodsTower");

        var state = MichonneSaves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.Contains("Episode 1", state.Unavailable);
        Assert.Empty(MichonneSaves.Handler.GetCarriedItems(slot));
    }

    [Fact]
    public void CheckpointThatStartsAnEpisode_HasNoInventoryToEdit()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        MichonneSaves.Handler.RestartFromEpisode(slot, 1);

        var state = MichonneSaves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.Contains("Resume Point", state.Unavailable);
        Assert.Throws<InvalidOperationException>(() => MichonneSaves.Handler.SetInventory(slot, Held(Machete)));
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
