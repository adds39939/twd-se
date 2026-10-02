using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Core.Tests.Integration;

public class Season1ChapterTests
{
    private const string ArmFlag = "1Morgue - Cut Off Arm";
    private const string ArmChoice = "Cut Off Arm";

    [Theory]
    [InlineData("logic_game", 0x1D3802238E8CE045)]
    [InlineData("logic_checkpoint", 0xBFA39BD9297F70DA)]
    [InlineData("logic_saveload", 0xBFC806883BAFB11F)]
    [InlineData("logic_inventory_items", 0x2B7DBB8D7F96FB38)]
    [InlineData("logic_module_script", 0xFDA9856B5C32F67B)]
    public void LogicAgentProperties_AreNamedLikeTheGameNamesThem(string agent, ulong expected)
    {
        Assert.Equal(expected, S1RuntimeProperties.LogicName(agent));
    }

    [Fact]
    public void SceneAgentProperties_AreNamedLikeTheGameNamesThem()
    {
        Assert.Equal(0x21541CE2D9D0CDB6UL, S1RuntimeProperties.Name("adv_jewelryStore.scene", "adv_jewelryStore.scene"));
        Assert.Equal(0x0C4BF380DAE15D87UL, S1RuntimeProperties.Name("Lee", "adv_jewelryStore.scene"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Catalog_EveryChapterAfterTheFirstHasAWayIn(int episode)
    {
        var chapters = S1ChapterCatalog.ForEpisode(episode)!.Chapters;

        Assert.True(chapters.Count >= 18);
        Assert.True(chapters[0].StartsEpisode);
        Assert.Equal(chapters.Count, chapters.Select(chapter => chapter.Id).Distinct().Count());
        foreach (var chapter in chapters.Skip(1))
        {
            Assert.NotNull(chapter.Entry);
            Assert.EndsWith(".lua", chapter.Entry.Script);
            if (chapter.Entry.Dialog == null)
                continue;

            Assert.EndsWith(".scene", chapter.Entry.Scene);
            Assert.StartsWith("dlg_id: ", chapter.Entry.Node);
            Assert.NotEqual(chapter.Script, chapter.Entry.Script, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Catalog_HasNoChaptersFor400Days()
    {
        Assert.Empty(Season1Saves.Handler.GetChapters(S1ResumePoint.ExtraEpisode));
    }

    [Fact]
    public void RestartFromChapter_WritesACheckpointThatJumpsFromThePreviousScene()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnJewelryStore");

        var autosave = Season1Saves.Reload(slot).Autosave!;
        var save = SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data);
        var checkpoint = Properties(autosave, S1RuntimeProperties.LogicName(S1RuntimeProperties.CheckpointAgent));

        Assert.Equal("env_marshHouseExterior.lua", save.LuaDoFile);
        Assert.Empty(save.Agents);
        Assert.Equal(save.RuntimePropertyNames.Order(), autosave.Files.Skip(2).Select(file => file.NameSymbol).Order());
        Assert.Equal(
            new[] { "MenuSeason1", "ProjectSeason1", "WalkingDead105" }.Select(TelltaleHash.ComputeCrc64).Order(),
            save.EnabledDynamicSets.Order());
        Assert.Equal("dlg_id: 17095284574668610419", checkpoint.GetString(S1CheckpointBuilder.CheckpointDialogItem));
        Assert.Equal("WalkingDead105", autosave.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal(3, Properties(autosave, S1SlotFiles.LogicGameProperties).GetInt("nAct"));
    }

    [Fact]
    public void RestartFromChapter_PointsTheSlotAtTheCheckpoint()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnJewelryStore");

        var reloaded = Season1Saves.Reload(slot);
        var state = Season1Saves.Handler.GetResumeState(reloaded);

        Assert.Equal(Season1Saves.Autosave, reloaded.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal("WalkingDead105", reloaded.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(reloaded.Metadata.GetInt(SlotMetadataKeys.LatestSerial), reloaded.Autosave!.Metadata!.GetInt(SaveMetadataKeys.Serial));
        Assert.DoesNotContain(Season1Saves.Autosave, slot.ObsoleteFileNames);
        Assert.False(reloaded.AutosaveDamaged);
        Assert.Equal(5, state.Episode);
        Assert.Equal("Jewelry Store", state.Checkpoint);
    }

    [Fact]
    public void RestartFromChapter_AtTheFirstChapterIsAPlainEpisodeRestart()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnMorgue");

        Assert.Null(slot.Autosave);
        Assert.Equal(string.Empty, slot.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Contains(Season1Saves.Autosave, slot.ObsoleteFileNames);
    }

    [Fact]
    public void RestartFromChapter_CarriesTheDecisionsOfEarlierAndCurrentEpisodes()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue("Lost Temper", "true");

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnJewelryStore");

        var logic = Properties(slot.Autosave!, S1SlotFiles.LogicGameProperties);
        Assert.Equal("carley", logic.GetString("DougCarley Saved"));
        Assert.Equal("true", logic.GetString("Saved Ben"));
        Assert.Equal("true", logic.GetString("Lost Temper"));
    }

    [Theory]
    [InlineData("OnMorgueArmChop", "true", null)]
    [InlineData("OnElevatorShaft", "true", true)]
    [InlineData("OnJewelryStore", "true", true)]
    [InlineData("OnJewelryStore", "false", false)]
    public void RestartFromChapter_SetsTheArmFlagOnlyAfterThatDecision(string chapter, string choice, bool? expected)
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue(ArmChoice, choice);

        Season1Saves.Handler.RestartFromChapter(slot, 5, chapter);

        Assert.Equal(expected, Properties(slot.Autosave!, S1SlotFiles.LogicGameProperties).GetBool(ArmFlag));
    }

    [Fact]
    public void ChangingAChoice_RebuildsAGeneratedCheckpoint()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue(ArmChoice, "true");
        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnJewelryStore");

        var reloaded = Season1Saves.Reload(slot);
        Season1Saves.Accessor(reloaded).SetChoiceValue(ArmChoice, "false");

        var logic = Properties(Season1Saves.Reload(reloaded).Autosave!, S1SlotFiles.LogicGameProperties);
        Assert.False(logic.GetBool(ArmFlag));
        Assert.Equal("false", logic.GetString(ArmChoice));
        Assert.Equal(3, logic.GetInt("nAct"));
    }

    [Fact]
    public void RestartFromChapter_ForcesTheDialogFileOfTheSceneItJumpsFrom()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnRooftop");

        var autosave = slot.Autosave!;
        var scene = Properties(autosave, 0x08155E7F36225F06);
        Assert.NotNull(Season1Saves.Reload(slot).Autosave!.FindFile(0x08155E7F36225F06));
        Assert.Equal("env_morgue.lua", SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data).LuaDoFile);
        Assert.Equal(Symbol.FromString("env_elevatorShaft.dlog"), scene.Find("Dialog Agent - File Primary")!.Value.BoxedValue);
    }

    [Fact]
    public void RestartFromChapter_PutsInventoryItemsInTheInventoryProperties()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnManorAttic");

        var inventory = Properties(slot.Autosave!, S1RuntimeProperties.LogicName("logic_inventory_items"));
        Assert.Equal(1, inventory.GetInt("Inventory - Cleaver"));
        Assert.Equal(2, Properties(slot.Autosave!, S1SlotFiles.LogicGameProperties).GetInt("nAct"));
    }

    [Theory]
    [InlineData("OnMarshHouseExterior2", "true", 0)]
    [InlineData("OnMarshHouseExterior2", "false", 1)]
    [InlineData("OnMarshHouseInterior", "true", 1)]
    public void RestartFromChapter_TakesTheCleaverAwayOnceItWasSurrendered(string chapter, string surrendered, int expected)
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Accessor(slot).SetChoiceValue("Surrendered Cleaver", surrendered);

        Season1Saves.Handler.RestartFromChapter(slot, 5, chapter);

        var inventory = Properties(slot.Autosave!, S1RuntimeProperties.LogicName("logic_inventory_items"));
        Assert.Equal(expected, inventory.GetInt("Inventory - Cleaver"));
    }

    [Fact]
    public void RestartFromChapter_InsideTheFirstSceneGoesThroughTheRecap()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromChapter(slot, 5, "OnElevatorShaft");

        var autosave = slot.Autosave!;
        Assert.Equal("PreviouslyOn.lua", SaveGameCodec.Read(autosave.FindFile(BundleFileNames.SaveGame)!.Data).LuaDoFile);
        Assert.True(Properties(autosave, S1SlotFiles.LogicGameProperties).GetBool("1Morgue - Complete"));
        Assert.Equal(string.Empty, Properties(autosave, S1RuntimeProperties.LogicName(S1RuntimeProperties.CheckpointAgent)).GetString(S1CheckpointBuilder.CheckpointDialogItem));
    }

    [Fact]
    public void GeneratedCheckpoint_IsWrittenTheSameWayAfterReloading()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        Season1Saves.Handler.RestartFromChapter(slot, 2, "OnDairyMeatLocker");

        var written = BundleWriter.Write(slot.Autosave!);
        var rewritten = BundleWriter.Write(BundleReader.Read(written, Season1Saves.Autosave));

        Assert.Equal(written, rewritten);
    }

    [Fact]
    public void RestartFromChapter_RejectsAnUnknownChapter()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Assert.Throws<ArgumentException>(() => Season1Saves.Handler.RestartFromChapter(slot, 5, "OnNowhere"));
        Assert.Throws<ArgumentException>(() => Season1Saves.Handler.RestartFromChapter(slot, S1ResumePoint.ExtraEpisode, "OnMorgue"));
    }

    private static PropertySet Properties(SaveSlot autosave, ulong symbol)
    {
        var file = autosave.FindFile(symbol);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
