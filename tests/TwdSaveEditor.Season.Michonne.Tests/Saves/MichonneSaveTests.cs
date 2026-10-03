using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Season.Michonne.Story;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.Michonne.Tests.Saves;

public class MichonneSaveTests
{
    private const string EndIt = "Episode 101 - Did you try to end it";
    private const string Ferry = "Episode 101 - How did you enter the abandoned ferry";
    private const string Randall = "Episode 102 - What did you do to Randall";
    private const string ZachDead = "Episode 101 - Zach is Dead";

    [Fact]
    public void RealSave_LoadsItsLogAndSaves()
    {
        var slot = MichonneSaves.LoadEpisode1Save();

        Assert.NotNull(slot.EventLog);
        Assert.Equal(2, slot.EventLog.PageFiles.Count);
        Assert.Equal([MichonneSaves.Autosave, MichonneSaves.Checkpoint], slot.Checkpoints.Select(save => save.FileName).Order());

        var state = MichonneSaves.Handler.GetResumeState(slot);
        Assert.Equal(1, state.Episode);
        Assert.False(state.StartsFromBeginning);
        Assert.Equal("Flagship Interior Escape", state.Checkpoint);
        Assert.Empty(MichonneSaves.Handler.BuildCompanionFiles(slot));
    }

    [Fact]
    public void DetectCurrentChoices_ReadsEveryDecisionAsDetectCurrentChoiceDoes()
    {
        var accessor = MichonneSaves.Accessor(MichonneSaves.LoadEpisode1Save());
        var choices = MichonneSaves.Handler.Choices;

        var states = accessor.DetectCurrentChoices(choices);

        Assert.Equal(choices.Select(accessor.DetectCurrentChoice), states);
        Assert.Contains(-1, states);
        Assert.Contains(states, state => state >= 0);
    }

    [Fact]
    public void RealSave_ReadsTheDecisionsThatWerePlayed()
    {
        var accessor = MichonneSaves.Accessor(MichonneSaves.LoadEpisode1Save());

        var made = MichonneStory.Season.Decisions.Where(decision => decision.Episode == 1 && accessor.GetChoiceValue(decision.ChoiceKey) != null).ToList();

        Assert.True(made.Count >= 10, $"Only {made.Count} Episode 1 decisions read from the real log.");
        Assert.Contains(accessor.GetChoiceValue(EndIt), new[] { "pulled_the_trigger", "lowered_the_gun" });
        Assert.Contains(accessor.GetChoiceValue(Ferry), new[] { "climbed_the_ladder", "went_in_through_the_window" });
    }

    [Theory]
    [InlineData(EndIt, "pulled_the_trigger")]
    [InlineData(EndIt, "lowered_the_gun")]
    [InlineData(Ferry, "climbed_the_ladder")]
    [InlineData(Ferry, "went_in_through_the_window")]
    [InlineData(Randall, "bashed_randall_s_head_in")]
    [InlineData(Randall, "showed_him_mercy")]
    [InlineData(ZachDead, "true")]
    [InlineData(ZachDead, "false")]
    public void ChangedDecision_IsReadBackAfterTheLogIsRewritten(string key, string value)
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        var others = MichonneStory.Season.Decisions.Where(decision => decision.ChoiceKey != key)
            .ToDictionary(decision => decision.ChoiceKey, decision => MichonneSaves.Accessor(slot).GetChoiceValue(decision.ChoiceKey));

        MichonneSaves.Accessor(slot).SetChoiceValue(key, value);

        var accessor = MichonneSaves.Accessor(MichonneSaves.Reload(slot));
        Assert.Equal(value, accessor.GetChoiceValue(key));
        var changed = others.Where(other => accessor.GetChoiceValue(other.Key) != other.Value).Select(other => other.Key).ToList();
        Assert.True(changed.Count <= 1, $"Setting {key} also changed {string.Join(", ", changed)}.");
    }

    [Fact]
    public void KeyDefinedForTwoEpisodes_ReadsTheSameUnderBothDefinitions()
    {
        var slot = MichonneSaves.LoadEpisode1Save();

        foreach (var value in new[] { "true", "false", "true" })
        {
            MichonneSaves.Accessor(slot).SetChoiceValue(ZachDead, value);

            var nodes = new StoryEventLog(slot, MichonneStory.Season).Nodes();
            var definitions = MichonneStory.Season.LogicKeys.Where(key => key.Key == ZachDead).ToList();
            Assert.Equal(2, definitions.Count);
            Assert.All(definitions, key => Assert.Equal(bool.Parse(value), StoryDecisionLog.Evaluate(key, nodes)));
        }
    }

    [Fact]
    public void RestartFromALaterEpisode_KeepsEarlierSavesAndMarksThemFinished()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        var made = MichonneSaves.Accessor(slot).GetChoiceValue(EndIt);

        MichonneSaves.Handler.RestartFromEpisode(slot, 2);

        var reloaded = MichonneSaves.Reload(slot);
        var metadata = reloaded.Metadata!;
        Assert.Equal(2, reloaded.Checkpoints.Count);
        Assert.Equal(2, metadata.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal("1", metadata.GetString(StoryFiles.LastEpisodeFinished));
        Assert.True(metadata.GetBool(SlotMetadataKeys.CompletedEpisode(1)));
        Assert.Equal(1, MichonneStory.Resume.LastFinished(reloaded));
        Assert.Contains(reloaded.EventLog!.Events, entry => entry.Number(EventLogEventTypes.EndEpisode) == 1);
        Assert.Equal(made, MichonneSaves.Accessor(reloaded).GetChoiceValue(EndIt));

        var state = MichonneSaves.Handler.GetResumeState(reloaded);
        Assert.Equal(2, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Fact]
    public void RestartFromTheFirstEpisode_LeavesACheckpointSoTheSlotIsNotListedAsEmpty()
    {
        var slot = MichonneSaves.LoadEpisode1Save();

        MichonneSaves.Handler.RestartFromEpisode(slot, 1);

        Assert.Contains(MichonneSaves.Autosave, slot.ObsoleteFileNames);
        var reloaded = MichonneSaves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        Assert.Equal(MichonneSaves.Checkpoint, save.FileName);
        Assert.Equal("ShoreLineCove", save.Metadata!.GetString(StoryFiles.SavedScript));
        Assert.Equal("101_chapter1", save.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Null(save.FindFile(StoryFiles.ScriptProperties));
        Assert.Null(save.FindFile(StoryFiles.LogicGameProperties));
        Assert.Equal(MichonneSaves.Checkpoint, reloaded.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal("0", reloaded.Metadata.GetString(StoryFiles.LastEpisodeFinished));
        Assert.DoesNotContain(reloaded.EventLog!.Events, entry => entry.DialogNode != null);

        var state = MichonneSaves.Handler.GetResumeState(reloaded);
        Assert.Equal(1, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Fact]
    public void ResumeFromAChapter_WritesACheckpointThatStartsTheSceneLikeTheDeveloperMenu()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        var accessor = MichonneSaves.Accessor(slot);
        var endIt = accessor.GetChoiceValue(EndIt);

        MichonneSaves.Handler.RestartFromChapter(slot, 1, "FerryInteriorSnackBar");

        var reloaded = MichonneSaves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var metadata = save.Metadata!;
        Assert.Equal(MichonneSaves.Checkpoint, save.FileName);
        Assert.Equal(1, metadata.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal(24, metadata.GetInt(SaveMetadataKeys.Serial));
        Assert.Equal("101_chapter4", metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal("WalkingDeadM101", metadata.GetString(StoryFiles.SavedProject));
        Assert.Equal("FerryInterior", metadata.GetString(StoryFiles.SavedScript));

        var game = SaveGameCodec.Read(save.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal("FerryInterior.lua", game.LuaDoFile);
        Assert.Equal(save.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Equal(4, game.EnabledDynamicSets.Count);

        Assert.Equal("DebugMenu", Runtime(save, StoryFiles.ScriptProperties).GetString(StoryCheckpointBuilder.PreviousScript));
        Assert.Equal("101_chapter4", Runtime(save, StoryFiles.SaveLoadProperties).GetString(StoryCheckpointBuilder.ChapterId));
        Assert.True(Runtime(save, StoryFiles.LogicGameProperties).GetBool("2FerryInterior - In Snack Bar"));

        Assert.Equal(MichonneSaves.Checkpoint, reloaded.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(24, reloaded.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
        Assert.Equal(24, reloaded.EventLog!.Events.Last().SaveSerial);

        var after = MichonneSaves.Accessor(reloaded);
        Assert.Equal(endIt, after.GetChoiceValue(EndIt));
        Assert.Null(MichonneStory.Season.Decisions
            .Where(decision => decision.ChoiceKey == "Episode 101 - Did you let Sam shoot Zachary")
            .Select(decision => new StoryDecisionLog(reloaded, MichonneStory.Season).IsSet(decision) ? decision : null)
            .Single());

        var state = MichonneSaves.Handler.GetResumeState(reloaded);
        Assert.Equal("Ferry Interior - Snack Bar", state.Checkpoint);
        Assert.False(state.StartsFromBeginning);
    }

    [Fact]
    public void ResumeFromAChapterOfALaterEpisode_CarriesTheStoryKeysThatEpisodeReads()
    {
        var slot = MichonneSaves.LoadEpisode1Save();
        MichonneSaves.Accessor(slot).SetChoiceValue(ZachDead, "true");

        MichonneSaves.Handler.RestartFromChapter(slot, 2, "WoodsTower");

        var save = slot.Checkpoints.Single(candidate => candidate.Metadata!.GetInt(SaveMetadataKeys.Episode) == 2);
        var logic = Runtime(save, StoryFiles.LogicGameProperties);
        Assert.True(logic.GetBool(ZachDead));
        Assert.Null(logic.Find("Episode 101 - Greg Zombified"));
        Assert.All(MichonneStory.Season.LogicKeys.Where(key => key.ReadFrom <= 2), key => Assert.NotNull(logic.Find(key.Key)));
        Assert.Equal("102_chapter3", save.Metadata!.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal(1, MichonneStory.Resume.LastFinished(slot));
        Assert.Equal(3, slot.Checkpoints.Count);
    }

    [Fact]
    public void NewSave_GetsALogAndACheckpointThatStartsItsEpisode()
    {
        var slot = TestSeasons.Registry.CreateSave("michonne", 2, "wdm_saveslot5.bundle");
        slot.DetectedSeasonKey = "michonne";

        var save = Assert.Single(slot.Checkpoints);
        Assert.Equal("_wdm_saveslot5_checkpoint1.bundle", save.FileName);
        Assert.Equal("PreviouslyOn", save.Metadata!.GetString(StoryFiles.SavedScript));
        Assert.Equal(2, save.Metadata.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal(2, slot.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal("1", slot.Metadata.GetString(StoryFiles.LastEpisodeFinished));
        Assert.False(new StoryEventLog(slot, MichonneStory.Season).HasPreviousGameData);

        var files = MichonneSaves.Handler.BuildCompanionFiles(slot);
        Assert.Equal(["_wdm_saveslot5_checkpoint1.bundle", "_wdm_saveslot5_id.estore"], files.Select(file => file.Name).Order());

        var state = MichonneSaves.Handler.GetResumeState(slot);
        Assert.Equal(2, state.Episode);
        Assert.True(state.StartsFromBeginning);
        Assert.All(MichonneStory.Season.Decisions.Where(decision => decision.Episode == 1), decision =>
            Assert.NotNull(MichonneSaves.Accessor(slot).GetChoiceValue(decision.ChoiceKey)));
    }

    [Fact]
    public void ChapterList_CoversEveryEpisode()
    {
        Assert.Equal([1, 2, 3], MichonneStory.Season.Chapters.Select(episode => episode.Episode));
        foreach (var episode in MichonneStory.Season.Chapters)
        {
            Assert.True(episode.Chapters[0].StartsEpisode);
            Assert.Single(episode.Chapters, chapter => chapter.StartsEpisode);
            Assert.Equal(episode.Chapters.Count, episode.Chapters.Select(chapter => chapter.Id).Distinct().Count());
            Assert.All(episode.Chapters, chapter => Assert.Matches($"^10{episode.Episode}_chapter\\d+$", chapter.ChapterId));
        }

        Assert.DoesNotContain(MichonneStory.Season.Chapters[0].Chapters, chapter => chapter.Title.EndsWith(".dlog", StringComparison.Ordinal));
        Assert.DoesNotContain(MichonneStory.Season.Chapters[0].Chapters, chapter => chapter.Script is "FlagshipExteriorEscape" or "BoatTownEscape");
        Assert.Equal(21, MichonneStory.Season.Chapters[0].Chapters.Count);
        Assert.Equal(5, MichonneSaves.Handler.GetChapters(1).Select(chapter => chapter.Group).Distinct().Count() + 2);
    }

    private static PropertySet Runtime(SaveSlot save, ulong name)
    {
        var file = save.FindFile(name);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
