using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Core.Tests.Integration;

public class Season1InventoryTests
{
    private const string InventoryAgent = "logic_inventory_items";
    private const string LockerCombination = "Inventory - Locker Combination";
    private const string IceAxe = "Inventory - Ice Axe";
    private const string Cleaver = "Inventory - Cleaver";
    private const string Food = "Drugstore - PlayerFood";
    private const string OfficeKeys = "Drugstore - Got Keys";
    private const string WeaponChoice = "Weapon Choice";

    [Fact]
    public void RealSave_ListsWhatLeeHoldsAndTheItemsOfItsEpisode()
    {
        var state = Season1Saves.Handler.GetInventory(Season1Saves.LoadEpisode4Save());

        Assert.True(state.Editable);
        Assert.Equal("Lee", state.Owner);
        Assert.Equal(4, state.Episode);
        Assert.Equal(new HeldItem(LockerCombination), Assert.Single(state.Held));
        Assert.Equal(16, state.Items.Count);
        Assert.Equal("Ice Axe", state.Items.Single(item => item.Id == IceAxe).Name);
        Assert.False(state.CarriedItemsKnown);
        Assert.Empty(Season1Saves.Handler.GetCarriedItems(Season1Saves.LoadEpisode4Save()));
    }

    [Fact]
    public void ChangingTheItemsOfARealSave_RewritesOnlyItsInventory()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        var original = BundleReader.Read(Season1Saves.ReadBytes(Season1Saves.Autosave), Season1Saves.Autosave);

        Season1Saves.Handler.SetInventory(slot, [new HeldItem(IceAxe)]);

        var reloaded = Season1Saves.Reload(slot);
        var inventory = Properties(reloaded.Autosave!, InventoryAgent);
        Assert.Equal(new HeldItem(IceAxe), Assert.Single(Season1Saves.Handler.GetInventory(reloaded).Held));
        Assert.Equal(1, inventory.GetInt(IceAxe));
        Assert.Equal(0, inventory.GetInt(LockerCombination));

        var changed = original.Files.Zip(reloaded.Autosave!.Files)
            .Where(pair => !pair.First.Data.AsSpan().SequenceEqual(pair.Second.Data))
            .Select(pair => pair.First.NameSymbol);
        Assert.Equal(original.Files.Count, reloaded.Autosave.Files.Count);
        Assert.Equal([S1RuntimeProperties.LogicName(InventoryAgent)], changed);
    }

    [Fact]
    public void SaveThatStartsAnEpisode_HasNoInventoryToEdit()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Handler.RestartFromEpisode(slot, 3);

        var state = Season1Saves.Handler.GetInventory(slot);

        Assert.False(state.Editable);
        Assert.NotNull(state.Unavailable);
        Assert.Throws<InvalidOperationException>(() => Season1Saves.Handler.SetInventory(slot, [new HeldItem(IceAxe)]));
    }

    [Fact]
    public void ChapterCheckpoint_ShowsTheItemsOfTheDevelopersSetup()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue("Surrendered Cleaver", "false");

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnMarshHouseExterior2");

        var state = Season1Saves.Handler.GetInventory(slot);
        Assert.Equal(5, state.Episode);
        Assert.True(state.CarriedItemsKnown);
        Assert.Equal(new HeldItem(Cleaver), Assert.Single(state.Held));
    }

    [Theory]
    [InlineData("inventory - spike remover", "Inventory - Spike Remover")]
    [InlineData("inventory - monkey wrench", "Inventory - Monkey Wrench")]
    [InlineData("inventory - spanner", "Inventory - Spanner")]
    public void CarriedWeapon_FollowsTheWeaponDecision(string choice, string expected)
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue(WeaponChoice, choice);
        Season1Saves.Handler.RestartFromChapter(slot, 3, "OnOverpassI");
        Assert.Empty(Season1Saves.Handler.GetInventory(slot).Held);

        var carried = Season1Saves.Handler.GetCarriedItems(slot);
        Season1Saves.Handler.SetInventory(slot, carried);

        Assert.Equal(new HeldItem(expected), Assert.Single(carried));

        var autosave = slot.Autosave!;
        var game = SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal(autosave.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Contains(S1RuntimeProperties.LogicName(InventoryAgent), game.RuntimePropertyNames);

        var reloaded = Season1Saves.Reload(slot);
        Assert.Equal(1, Properties(reloaded.Autosave!, InventoryAgent).GetInt(expected));
        Assert.Equal(new HeldItem(expected), Assert.Single(Season1Saves.Handler.GetInventory(reloaded).Held));
        Assert.Equal("Overpass I", Season1Saves.Handler.GetResumeState(reloaded).Checkpoint);
    }

    [Fact]
    public void CountedAndFlagItems_AreWrittenInTheGameLogic()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Handler.RestartFromChapter(slot, 1, "OnDrugstoreOffice");

        Season1Saves.Handler.SetInventory(slot, [new HeldItem(Food, 3), new HeldItem(OfficeKeys), new HeldItem("Inventory - Axe", 5)]);

        var reloaded = Season1Saves.Reload(slot);
        var state = Season1Saves.Handler.GetInventory(reloaded);
        Assert.Equal(4, state.Items.Single(item => item.Id == Food).MaxCount);
        Assert.Equal(3, state.CountOf(Food));
        Assert.Equal(1, state.CountOf(OfficeKeys));
        Assert.Equal(1, state.CountOf("Inventory - Axe"));
        Assert.Equal(3, Season1Saves.LogicGame(reloaded).GetInt(Food));
        Assert.True(Season1Saves.LogicGame(reloaded).GetBool(OfficeKeys));
        Assert.Equal(1, Properties(reloaded.Autosave!, InventoryAgent).GetInt("Inventory - Axe"));
    }

    [Fact]
    public void ChangingAChoice_KeepsTheItemsOfAGeneratedCheckpoint()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue("Surrendered Cleaver", "false");
        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnMarshHouseExterior2");
        Season1Saves.Handler.SetInventory(slot, [new HeldItem(Cleaver), new HeldItem("Inventory - Bat")]);

        Season1Saves.Accessor(slot).SetChoiceValue("Surrendered Cleaver", "true");

        var state = Season1Saves.Handler.GetInventory(slot);
        Assert.Equal(new HeldItem("Inventory - Bat"), Assert.Single(state.Held));
    }

    [Fact]
    public void ItemData_CoversEveryChapterWithItemsOfItsEpisode()
    {
        Assert.Equal([1, 2, 3, 4, 5, 6], S1ItemCatalog.All.Select(episode => episode.Episode));
        Assert.Equal([17, 15, 17, 16, 5, 2], S1ItemCatalog.All.Select(episode => episode.Items.Count));

        foreach (var episode in S1ItemCatalog.All)
        {
            var ids = episode.Items.Select(item => item.Id).ToList();
            Assert.Equal(S1ChapterCatalog.ForEpisode(episode.Episode)!.Chapters.Select(chapter => chapter.Id), episode.Chapters.Select(chapter => chapter.Id));
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(episode.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
            Assert.All(episode.Items, item => Assert.True(item.MaxCount >= 1));
            Assert.All(episode.Chapters.SelectMany(chapter => chapter.Carried), item => Assert.Contains(item, ids));
        }
    }

    private static PropertySet Properties(SaveSlot save, string agent)
    {
        var file = save.FindFile(S1RuntimeProperties.LogicName(agent));
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
