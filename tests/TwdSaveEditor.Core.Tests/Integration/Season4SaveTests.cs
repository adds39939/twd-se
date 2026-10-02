using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.S4.Story;

namespace TwdSaveEditor.Core.Tests.Integration;

public class Season4SaveTests
{
    private const string Ending = "Episode 205 - Ending Choice";
    private const string KilledLee = "Episode 105 - Killed Lee";
    private const string Hunting = "Episode 401 - Hunting vs Fishing";
    private const string AbelFood = "Episode 401 - Abel Food";
    private const string Saved = "Episode 402 - Save Violet or Louis";
    private const string HappyCouple = "Episode 401 - Happy Couple";

    [Fact]
    public void RealSave_LoadsItsLogAndAutosave()
    {
        var slot = Season4Saves.LoadEpisode1Save();

        Assert.NotNull(slot.EventLog);
        Assert.Equal(4, slot.EventLog.PageFiles.Count);
        Assert.Equal(Season4Saves.Autosave, Assert.Single(slot.Checkpoints).FileName);

        var state = Season4Saves.Handler.GetResumeState(slot);
        Assert.Equal(1, state.Episode);
        Assert.Equal("Boarding School Interior", state.Checkpoint);
        Assert.Empty(Season4Saves.Handler.BuildCompanionFiles(slot));
    }

    [Fact]
    public void RealSave_ReadsEveryStoryKeyAsTheGameStoredIt()
    {
        var slot = Season4Saves.LoadEpisode1Save();
        var game = Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties);
        var nodes = new StoryEventLog(slot, S4Story.Season).Nodes();

        var compared = 0;
        foreach (var key in S4Story.Season.LogicKeys.Where(key => key.ReadFrom <= 1))
        {
            switch (game.Find(key.Key)?.Value)
            {
                case BoolValue flag:
                    Assert.Equal(flag.Value, StoryDecisionLog.Evaluate(key, nodes));
                    compared++;
                    break;
                case StringValue text:
                    Assert.Equal(text.Value, StoryDecisionLog.Evaluate(key, nodes));
                    compared++;
                    break;
            }
        }

        Assert.Equal(4, compared);
        Assert.True(new StoryEventLog(slot, S4Story.Season).HasPreviousGameData);
    }

    [Fact]
    public void RealSave_ReadsTheEarlierSeasonsFromTheImportedBlocks()
    {
        var accessor = Season4Saves.Accessor(Season4Saves.LoadEpisode1Save());

        Assert.Equal("alone", accessor.GetChoiceValue(Ending));
        Assert.Equal("true", accessor.GetChoiceValue(KilledLee));
        Assert.Equal("hair", accessor.GetChoiceValue("Episode 105 - Lee Advice"));
        Assert.Null(accessor.GetChoiceValue(Hunting));
        Assert.NotNull(accessor.GetChoiceValue(HappyCouple));
    }

    [Theory]
    [InlineData(Ending, "kenny")]
    [InlineData(KilledLee, "false")]
    [InlineData(Hunting, "went_fishing_with_violet_and")]
    [InlineData(Hunting, "went_hunting_with_louis_and")]
    [InlineData(AbelFood, "attacked_abel_rather_than_giving")]
    [InlineData(Saved, "rescued_louis_instead_of_violet")]
    public void ChangedDecision_IsReadBackAfterTheLogIsRewritten(string key, string value)
    {
        var slot = Season4Saves.LoadEpisode1Save();
        var others = S4Story.Season.Decisions.Where(decision => decision.ChoiceKey != key)
            .ToDictionary(decision => decision.ChoiceKey, decision => Season4Saves.Accessor(slot).GetChoiceValue(decision.ChoiceKey));

        Season4Saves.Accessor(slot).SetChoiceValue(key, value);

        var reloaded = Season4Saves.Reload(slot);
        var accessor = Season4Saves.Accessor(reloaded);
        Assert.Equal(value, accessor.GetChoiceValue(key));
        var changed = others.Where(other => accessor.GetChoiceValue(other.Key) != other.Value).Select(other => other.Key).ToList();
        Assert.True(changed.Count <= 1, $"Setting {key} also changed {string.Join(", ", changed)}.");
    }

    [Fact]
    public void ChangedEarlierSeasonOutcome_IsAlsoWrittenIntoTheSave()
    {
        var slot = Season4Saves.LoadEpisode1Save();

        Season4Saves.Accessor(slot).SetChoiceValue(Ending, "jane");

        var game = Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties);
        Assert.Equal("Jane", game.GetString(Ending));
        Assert.True(Assert.Single(slot.Checkpoints).Modified);
    }

    [Fact]
    public void RestartFromALaterEpisode_KeepsEarlierDecisionsAndMarksEpisodesFinished()
    {
        var slot = Season4Saves.LoadEpisode1Save();
        Season4Saves.Accessor(slot).SetChoiceValue(Hunting, "went_hunting_with_louis_and");

        Season4Saves.Handler.RestartFromEpisode(slot, 3);

        var reloaded = Season4Saves.Reload(slot);
        var metadata = reloaded.Metadata!;
        Assert.Equal(3, metadata.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(2, metadata.GetInt(StoryFiles.LastEpisodeFinished));
        Assert.True(metadata.GetBool(SlotMetadataKeys.CompletedEpisode(2)));
        Assert.Equal(2, S4Story.Resume.LastFinished(reloaded));
        Assert.Contains(reloaded.EventLog!.Events, entry => entry.Number(EventLogEventTypes.EndEpisode) == 2);
        Assert.Equal("went_hunting_with_louis_and", Season4Saves.Accessor(reloaded).GetChoiceValue(Hunting));
        Assert.All(S4Story.Season.Decisions.Where(decision => decision.Episode is 1 or 2), decision =>
            Assert.NotNull(Season4Saves.Accessor(reloaded).GetChoiceValue(decision.ChoiceKey)));

        var state = Season4Saves.Handler.GetResumeState(reloaded);
        Assert.Equal(3, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Fact]
    public void ResumeFromAChapter_WritesASaveThatStartsTheSceneLikeTheDeveloperMenu()
    {
        var slot = Season4Saves.LoadEpisode1Save();

        Season4Saves.Handler.RestartFromChapter(slot, 1, "TrainStation");

        var reloaded = Season4Saves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var metadata = save.Metadata!;
        Assert.Equal(Season4Saves.Autosave, save.FileName);
        Assert.Equal(1, metadata.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal("WalkingDead401", metadata.GetString(StoryFiles.SavedProject));
        Assert.Equal("TrainStation", metadata.GetString(StoryFiles.SavedScript));

        var game = SaveGameCodec.Read(save.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal("TrainStation.lua", game.LuaDoFile);
        Assert.Equal(save.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Equal(5, game.EnabledDynamicSets.Count);
        Assert.Equal(2, game.RuntimePropertyNames.Count);

        var systems = Runtime(save, StoryFiles.SystemsProperties);
        Assert.Equal("DebugMenu", systems.GetString(StoryCheckpointBuilder.PreviousScript));
        Assert.False(systems.GetBool("SaveLoad - Auto Save"));
        Assert.Equal("env_trainStation_act1.dlog", systems.GetString(StoryCheckpointBuilder.CheckpointDialogFile));
        Assert.Equal("cs_pitStop", systems.GetString(StoryCheckpointBuilder.CheckpointDialogNode));
        var logic = Runtime(save, StoryFiles.LogicGameProperties);
        Assert.Equal("Alone", logic.GetString(Ending));
        Assert.True(logic.GetBool(KilledLee));
        Assert.True(logic.GetBool("Runtime: Visible"));
        Assert.Null(logic.Find("Episode 401 - Heart Choice"));

        Assert.Equal("Train Station", Season4Saves.Handler.GetResumeState(reloaded).Checkpoint);
        Assert.Null(Season4Saves.Accessor(reloaded).GetChoiceValue(AbelFood));
    }

    [Fact]
    public void ResumeFromAChapterOfALaterEpisode_CarriesTheStoryKeysThatEpisodeReads()
    {
        var slot = Season4Saves.LoadEpisode1Save();
        Season4Saves.Accessor(slot).SetChoiceValue(Saved, "rescued_louis_instead_of_violet");

        Season4Saves.Handler.RestartFromChapter(slot, 3, "ForestCamp");

        var logic = Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties);
        Assert.All(S4Story.Season.LogicKeys.Where(key => key.ReadFrom <= 3), key => Assert.NotNull(logic.Find(key.Key)));
        Assert.All(S4Story.Season.LogicKeys.Where(key => key.ReadFrom > 3), key => Assert.Null(logic.Find(key.Key)));
        Assert.Equal(2, S4Story.Resume.LastFinished(slot));
        Assert.Equal("rescued_louis_instead_of_violet", Season4Saves.Accessor(slot).GetChoiceValue(Saved));
    }

    [Fact]
    public void NewSave_HasTheImportedBlockSoTheGameDoesNotListItAsEmpty()
    {
        var slot = TestSeasons.Registry.CreateSave("s4", 2, "wd4_saveslot3.bundle");
        slot.DetectedSeasonKey = "s4";

        Assert.True(new StoryEventLog(slot, S4Story.Season).HasPreviousGameData);
        Assert.Empty(slot.Checkpoints);
        Assert.Null(slot.ChoiceStats);
        Assert.Equal(2, slot.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(1, slot.Metadata.GetInt(StoryFiles.LastEpisodeFinished));
        Assert.Equal("_wd4_saveslot3_id.estore", Assert.Single(Season4Saves.Handler.BuildCompanionFiles(slot)).Name);
        Assert.All(S4Story.Season.Decisions.Where(decision => decision.Episode <= 1), decision =>
            Assert.NotNull(Season4Saves.Accessor(slot).GetChoiceValue(decision.ChoiceKey)));
    }

    [Fact]
    public void ImportFromSeason3_CopiesItsLogIntoTheBlock()
    {
        var source = Season3Saves.LoadEpisode1Save();
        var target = TestSeasons.Registry.CreateSave("s4", 1, "wd4_saveslot3.bundle");
        target.DetectedSeasonKey = "s4";
        Assert.True(Season4Saves.Handler.CanImportFrom(source));

        Season4Saves.Handler.ImportChoices(source, target);

        var accessor = Season4Saves.Accessor(target);
        Assert.Equal("alone", accessor.GetChoiceValue(Ending));
        Assert.Equal(
            source.EventLog!.Events.Select(entry => entry.DialogNode).OfType<ulong>().Distinct().Count(),
            target.EventLog!.Events.Count(entry => entry.DialogNode != null));
    }

    [Fact]
    public void Presets_UseDecisionsThatExist()
    {
        foreach (var preset in Season4Saves.Handler.Presets)
        {
            foreach (var selection in preset.Selections)
                Assert.NotNull(S4Story.Season.FindDecision(selection.ChoiceKey)?.Find(selection.Value));
        }
    }

    [Fact]
    public void ChapterList_CoversEveryEpisode()
    {
        Assert.Equal([1, 2, 3, 4], S4Story.Season.Chapters.Select(episode => episode.Episode));
        Assert.Equal(67, S4Story.Season.Chapters.Sum(episode => episode.Chapters.Count));
        Assert.All(S4Story.Season.Chapters.SelectMany(episode => episode.Chapters), chapter => Assert.False(string.IsNullOrEmpty(chapter.Dialog) || string.IsNullOrEmpty(chapter.DialogNode)));
        foreach (var episode in S4Story.Season.Chapters)
        {
            Assert.True(episode.Chapters[0].StartsEpisode);
            Assert.Single(episode.Chapters, chapter => chapter.StartsEpisode);
            Assert.Equal(episode.Chapters.Count, episode.Chapters.Select(chapter => chapter.Id).Distinct().Count());
        }

        Assert.DoesNotContain(S4Story.Season.Chapters[0].Chapters, chapter => chapter.Flags.Any(flag => flag.Key == "Debug ID"));
        Assert.Equal("Configurator", S4Story.Season.Chapters[0].Opening.Script);
    }

    private static PropertySet Runtime(SaveSlot save, ulong name)
    {
        var file = save.FindFile(name);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
