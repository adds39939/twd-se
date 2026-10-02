using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S2.Chapters;
using TwdSaveEditor.Season.S2.Inventory;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Core.Tests.Integration;

public class Season2InventoryTests
{
    private const string Watch = "ui_item_watch";
    private const string Hammer = "ui_item_hammer";
    private const string Lighter = "ui_item_lighter";
    private const string StoleWatch = "Episode 201 - Stole Watch";

    [Fact]
    public void RealSave_ListsWhatClementineHoldsAndTheItemsOfItsEpisode()
    {
        var state = Season2Saves.Handler.GetInventory(Season2Saves.LoadEpisode1Save());

        Assert.True(state.Editable);
        Assert.Equal("Clementine", state.Owner);
        Assert.Equal(1, state.Episode);
        Assert.Equal([Watch, Hammer], Ids(state.Held));
        Assert.Equal(13, state.Items.Count);
        Assert.Equal("Pocket Knife", state.Items.Single(item => item.Id == "ui_item_knifePocket").Name);
    }

    [Fact]
    public void ChangingTheItemsOfARealSave_RewritesOnlyItsInventory()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var before = Season2Saves.ReadBytes(Season2Saves.Autosave);

        Season2Saves.Handler.SetInventory(slot, Held(Hammer, Lighter));

        var reloaded = Season2Saves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var inventory = Runtime(save, S2SlotFiles.InventoryProperties);
        Assert.Equal([Hammer, Lighter], Ids(Season2Saves.Handler.GetInventory(reloaded).Held));
        Assert.True(inventory.GetBool(Lighter + S2Inventory.ShownSuffix));
        Assert.True(inventory.GetBool(Hammer + S2Inventory.ShownSuffix));
        Assert.False(inventory.GetBool(Watch + S2Inventory.ShownSuffix));
        Assert.False(Runtime(save, S2SlotFiles.LogicGameProperties).GetBool("bHasWatch"));
        Assert.True(Runtime(save, S2SlotFiles.LogicGameProperties).GetBool("bGotWatch"));

        var original = BundleReader.Read(before, Season2Saves.Autosave);
        Assert.Equal(original.Files.Count, save.Files.Count);
        var changed = original.Files.Zip(save.Files).Where(pair => !pair.First.Data.AsSpan().SequenceEqual(pair.Second.Data)).Select(pair => pair.First.NameSymbol);
        Assert.Equal(new[] { S2SlotFiles.InventoryProperties, S2SlotFiles.LogicGameProperties }.Order(), changed.Order());
    }

    [Fact]
    public void UntouchedRealInventory_IsWrittenBackByteForByte()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Season2Saves.Handler.SetInventory(slot, Held(Watch, Hammer));

        var written = Season2Saves.Handler.BuildCompanionFiles(slot).Single(file => file.Name == Season2Saves.Autosave).Data;
        var original = BundleReader.Read(Season2Saves.ReadBytes(Season2Saves.Autosave), Season2Saves.Autosave);
        var rewritten = BundleReader.Read(written, Season2Saves.Autosave);
        Assert.All(original.Files.Zip(rewritten.Files), pair => Assert.Equal(pair.First.Data, pair.Second.Data));
    }

    [Fact]
    public void SaveThatStartsAnEpisode_HasNoInventoryToEdit()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        Season2Saves.Handler.RestartFromEpisode(slot, 1);

        var state = Season2Saves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.NotNull(state.Unavailable);
        Assert.Empty(Season2Saves.Handler.GetCarriedItems(slot));
        Assert.Throws<InvalidOperationException>(() => Season2Saves.Handler.SetInventory(slot, Held(Hammer)));
    }

    [Fact]
    public void ChapterCheckpoint_GetsTheItemsPickedUpEarlierInTheEpisode()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        Season2Saves.Handler.RestartFromChapter(slot, 1, "CabinShedII");
        Assert.Empty(Season2Saves.Handler.GetInventory(slot).Held);

        var carried = Ids(Season2Saves.Handler.GetCarriedItems(slot));
        Season2Saves.Handler.SetInventory(slot, Held([.. carried]));

        string[] expected =
        [
            "ui_item_bandages", "ui_item_boxJuice", "ui_item_fishingLine", Hammer, "ui_item_needle", "ui_item_peroxide", Watch,
        ];
        Assert.Equal(expected, carried);

        var checkpoint = Assert.Single(slot.Checkpoints);
        var game = SaveGameCodec.Read(checkpoint.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal(checkpoint.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Contains(S2SlotFiles.InventoryProperties, game.RuntimePropertyNames);

        var reloaded = Season2Saves.Reload(slot);
        var inventory = Runtime(Assert.Single(reloaded.Checkpoints), S2SlotFiles.InventoryProperties);
        Assert.Equal(expected, inventory.GetStrings(S2Inventory.ItemsKey));
        Assert.Equal(expected, Ids(Season2Saves.Handler.GetInventory(reloaded).Held));
        Assert.All(expected, item => Assert.True(inventory.GetBool(item + S2Inventory.ShownSuffix)));
        Assert.Equal("Cabin Shed II", Season2Saves.Handler.GetResumeState(reloaded).Checkpoint);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void StartingItemsOfTheNextEpisode_FollowTheDecisionsOfTheEarlierOne(string stoleWatch, bool expected)
    {
        var slot = Season2Saves.LoadEpisode1Save();
        Season2Saves.Accessor(slot).SetChoiceValue(StoleWatch, stoleWatch);
        Season2Saves.Handler.RestartFromChapter(slot, 2, "LodgeRear");

        var carried = Ids(Season2Saves.Handler.GetCarriedItems(slot));

        Assert.Equal(expected, carried.Contains(Watch));
        Assert.Contains("ui_item_knifeSurvival", carried);
        Assert.Contains(Lighter, carried);
        Assert.Contains("ui_item_binoculars", carried);
        Assert.Contains("ui_item_bottleWater", carried);
        Assert.DoesNotContain(Hammer, carried);
        Assert.Equal(2, Season2Saves.Handler.GetInventory(slot).Episode);
        Assert.Equal(6, Season2Saves.Handler.GetInventory(slot).Items.Count);
    }

    [Fact]
    public void RemovingEveryItem_LeavesAnEmptyList()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Season2Saves.Handler.SetInventory(slot, []);

        var reloaded = Season2Saves.Reload(slot);
        Assert.Empty(Season2Saves.Handler.GetInventory(reloaded).Held);
        Assert.Empty(Runtime(Assert.Single(reloaded.Checkpoints), S2SlotFiles.InventoryProperties).GetStrings(S2Inventory.ItemsKey)!);
    }

    [Fact]
    public void ItemData_CoversEveryResumePointWithItemsOfItsEpisode()
    {
        Assert.Equal([1, 2, 3, 4, 5], S2ItemCatalog.All.Select(episode => episode.Episode));
        Assert.Equal([13, 6, 2, 5, 3], S2ItemCatalog.All.Select(episode => episode.Items.Count));

        foreach (var episode in S2ItemCatalog.All)
        {
            var ids = episode.Items.Select(item => item.Id).ToList();
            Assert.Equal(S2ChapterCatalog.ForEpisode(episode.Episode)!.Chapters.Select(chapter => chapter.Id), episode.Chapters.Select(chapter => chapter.Id));
            Assert.All(episode.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
            Assert.All(episode.Starting, item => Assert.Contains(item.Item, ids));
            Assert.All(episode.Chapters.SelectMany(chapter => chapter.Carried.Concat(chapter.FromStart)), item => Assert.Contains(item, ids));
        }
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
