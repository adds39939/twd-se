using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Season.S3.Story;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S3.Tests.Saves;

public class Season3SaveTests
{
    private const string Ending = "Episode 205 - Ending Choice";
    private const string Badger = "Episode 303 - How did Badger die";
    private const string BadgerKiller = "Episode 303 - Who Killed Badger";
    private const string Gabe = "Episode 305 - Did you go after Gabe or with Kate";
    private const string Clementine = "Episode 305 - Did Clementine come along with you";

    [Fact]
    public void RealSave_LoadsItsLogAndAutosave()
    {
        var slot = Season3Saves.LoadEpisode1Save();

        Assert.NotNull(slot.EventLog);
        Assert.Equal(4, slot.EventLog.PageFiles.Count);
        Assert.True(slot.EventLog.PageFiles.Single(page => page.Name == Season3Saves.Pages[1]).Page.Compressed);
        Assert.Equal(Season3Saves.Autosave, Assert.Single(slot.Checkpoints).FileName);

        var state = Season3Saves.Handler.GetResumeState(slot);
        Assert.Equal(1, state.Episode);
        Assert.False(state.StartsFromBeginning);
        Assert.Empty(Season3Saves.Handler.BuildCompanionFiles(slot));
    }

    [Fact]
    public void RealSave_ReadsEveryStoryKeyAsTheGameStoredIt()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var game = Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties);
        var nodes = new StoryEventLog(slot, S3Story.Season).Nodes();

        var compared = 0;
        foreach (var key in S3Story.Season.LogicKeys.Where(key => key.ReadFrom <= 1))
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

        Assert.True(compared >= 2);
        Assert.Equal("alone", Season3Saves.Accessor(slot).GetChoiceValue(Ending));
    }

    [Fact]
    public void ChangingASeasonTwoOutcome_RewritesTheLogAndTheSavedFlag()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var events = slot.EventLog!.Events.Count();

        Season3Saves.Accessor(slot).SetChoiceValue(Ending, "kenny");

        var reloaded = Season3Saves.Reload(slot);
        Assert.Equal("kenny", Season3Saves.Accessor(reloaded).GetChoiceValue(Ending));
        Assert.Equal(events, reloaded.EventLog!.Events.Count());
        Assert.Equal("Kenny", Runtime(Assert.Single(reloaded.Checkpoints), StoryFiles.LogicGameProperties).GetString(Ending));
    }

    [Fact]
    public void DecisionWithCompoundConditions_SetsAndClearsTheRightNodes()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var accessor = Season3Saves.Accessor(slot);

        accessor.SetChoiceValue(Badger, "killed_badger_quickly");
        Assert.Equal("killed_badger_quickly", accessor.GetChoiceValue(Badger));
        Assert.Equal("javier", accessor.GetChoiceValue(BadgerKiller));

        accessor.SetChoiceValue(Badger, "destroyed_badger_s_skull");
        Assert.Equal("destroyed_badger_s_skull", accessor.GetChoiceValue(Badger));
        Assert.Equal("javier", accessor.GetChoiceValue(BadgerKiller));

        accessor.SetChoiceValue(BadgerKiller, "tripp");
        Assert.Equal("let_someone_else_kill_badger", accessor.GetChoiceValue(Badger));

        var reloaded = Season3Saves.Reload(slot);
        Assert.Equal("tripp", Season3Saves.Accessor(reloaded).GetChoiceValue(BadgerKiller));
    }

    [Fact]
    public void DecisionsThatShareNodes_KeepEachOtherWhenOneChanges()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var accessor = Season3Saves.Accessor(slot);
        var gabe = TestSeasons.ChoicesFor("s3").Single(choice => choice.ChoiceKey == Gabe);
        var clementine = TestSeasons.ChoicesFor("s3").Single(choice => choice.ChoiceKey == Clementine);

        accessor.ApplyChoice(gabe, 1);
        accessor.ApplyChoice(clementine, 0);
        Assert.Equal(1, accessor.DetectCurrentChoice(gabe));
        Assert.Equal(0, accessor.DetectCurrentChoice(clementine));

        accessor.ApplyChoice(clementine, 1);
        Assert.Equal(1, accessor.DetectCurrentChoice(gabe));
        Assert.Equal(1, accessor.DetectCurrentChoice(clementine));

        accessor.ApplyChoice(gabe, 0);
        Assert.Equal(0, accessor.DetectCurrentChoice(gabe));
        Assert.Equal(1, accessor.DetectCurrentChoice(clementine));
    }

    [Fact]
    public void RestartFromALaterEpisode_FinishesTheEarlierOnesWithTheirDecisions()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        var before = Season3Saves.Accessor(slot).GetChoiceValue("Episode 301 - Kissed Kate");

        Season3Saves.Handler.RestartFromEpisode(slot, 3);

        var reloaded = Season3Saves.Reload(slot);
        var metadata = reloaded.Metadata!;
        var state = Season3Saves.Handler.GetResumeState(reloaded);
        var accessor = Season3Saves.Accessor(reloaded);

        Assert.Equal(1, Assert.Single(reloaded.Checkpoints).Metadata!.GetInt(SaveMetadataKeys.Episode));
        Assert.Empty(slot.ObsoleteFileNames);
        Assert.Equal(3, metadata.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(Season3Saves.Autosave, metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(2, S3Story.Resume.LastFinished(reloaded));
        Assert.True(metadata.GetBool(SlotMetadataKeys.CompletedEpisode(1)));
        Assert.True(metadata.GetBool(SlotMetadataKeys.CompletedEpisode(2)));
        Assert.Equal(3, state.Episode);
        Assert.True(state.StartsFromBeginning);

        var markers = reloaded.EventLog!.Events
            .Select(entry => (Begin: entry.Number(EventLogEventTypes.BeginEpisode), End: entry.Number(EventLogEventTypes.EndEpisode)))
            .Where(marker => marker.Begin != null || marker.End != null)
            .Select(marker => marker.Begin != null ? $"begin {marker.Begin}" : $"end {marker.End}")
            .ToList();
        Assert.Equal(["begin 1", "end 1", "begin 2", "end 2"], markers);

        Assert.Equal(before, accessor.GetChoiceValue("Episode 301 - Kissed Kate"));
        Assert.Equal("alone", accessor.GetChoiceValue(Ending));
        Assert.All(TestSeasons.ChoicesFor("s3").Where(choice => choice.Episode is 1 or 2), choice => Assert.True(accessor.DetectCurrentChoice(choice) >= 0));
        Assert.All(TestSeasons.ChoicesFor("s3").Where(choice => choice.Episode >= 3 && choice.Category == "Statistics"), choice => Assert.Equal(-1, accessor.DetectCurrentChoice(choice)));
    }

    [Fact]
    public void ResumeFromAChapter_WritesASaveThatOpensTheSceneLikeTheDeveloperMenu()
    {
        var slot = Season3Saves.LoadEpisode1Save();

        Season3Saves.Handler.RestartFromChapter(slot, 1, "VirginiaRoadTruck");

        var reloaded = Season3Saves.Reload(slot);
        var save = Assert.Single(reloaded.Checkpoints);
        var metadata = save.Metadata!;
        Assert.Equal(Season3Saves.Autosave, save.FileName);
        Assert.Equal(1, metadata.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal(22, metadata.GetInt(SaveMetadataKeys.Serial));
        Assert.Equal("WalkingDead301", metadata.GetString(StoryFiles.SavedProject));
        Assert.Equal("VirginiaRoad", metadata.GetString(StoryFiles.SavedScript));

        var game = SaveGameCodec.Read(save.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal("VirginiaRoad.lua", game.LuaDoFile);
        Assert.Equal(save.Files.Skip(2).Select(file => file.NameSymbol), game.RuntimePropertyNames);
        Assert.Equal(4, game.EnabledDynamicSets.Count);

        Assert.Equal("DebugMenu", Runtime(save, StoryFiles.ScriptProperties).GetString(StoryCheckpointBuilder.PreviousScript));
        var logic = Runtime(save, StoryFiles.LogicGameProperties);
        Assert.True(logic.GetBool("bEnteredJunkyardHill"));
        Assert.Equal("Alone", logic.GetString(Ending));
        Assert.False(logic.GetBool("Episode 205 - Rejected Family"));
        Assert.Null(logic.Find("Episode 301 - Kissed Kate"));

        Assert.Equal(Season3Saves.Autosave, reloaded.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(22, reloaded.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
        Assert.Equal(22, reloaded.EventLog!.Events.Last().SaveSerial);
        Assert.DoesNotContain(Season3Saves.Autosave, slot.ObsoleteFileNames);

        var state = Season3Saves.Handler.GetResumeState(reloaded);
        Assert.Equal(1, state.Episode);
        Assert.Equal("Virginia Road - Truck", state.Checkpoint);
    }

    [Fact]
    public void ResumeFromAFlashbackChapter_SetsTheEndingItBelongsTo()
    {
        var slot = Season3Saves.LoadEpisode1Save();

        Season3Saves.Handler.RestartFromChapter(slot, 1, "HardwareStore");

        Assert.Equal("jane", Season3Saves.Accessor(slot).GetChoiceValue(Ending));
        Assert.Equal("Jane", Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties).GetString(Ending));
    }

    [Fact]
    public void ResumeFromAChapterOfALaterEpisode_CarriesTheStoryKeysThatEpisodeReads()
    {
        var slot = Season3Saves.LoadEpisode1Save();
        Season3Saves.Accessor(slot).SetChoiceValue("Episode 302 - Shot Conrad", "true");

        Season3Saves.Handler.RestartFromChapter(slot, 3, "RichmondChurch");

        var logic = Runtime(Assert.Single(slot.Checkpoints), StoryFiles.LogicGameProperties);
        Assert.True(logic.GetBool("Episode 302 - Shot Conrad"));
        Assert.Equal(3, Assert.Single(slot.Checkpoints).Metadata!.GetInt(SaveMetadataKeys.Episode));
        Assert.All(S3Story.Season.LogicKeys.Where(key => key.ReadFrom <= 3), key => Assert.NotNull(logic.Find(key.Key)));
        Assert.All(S3Story.Season.LogicKeys.Where(key => key.ReadFrom > 3), key => Assert.Null(logic.Find(key.Key)));
        Assert.Equal(2, S3Story.Resume.LastFinished(slot));
    }

    [Fact]
    public void NewSave_HasTheSeasonTwoBlockSoTheGameDoesNotListItAsEmpty()
    {
        var slot = TestSeasons.Registry.CreateSave("s3", 3, "wd3_saveslot2.bundle");
        slot.DetectedSeasonKey = "s3";

        Assert.True(new StoryEventLog(slot, S3Story.Season).HasPreviousGameData);
        Assert.Empty(slot.Checkpoints);
        Assert.True(Assert.Single(slot.Files).IsNamed(BundleFileNames.SlotMetadata));
        Assert.Equal(3, slot.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(2, S3Story.Resume.LastFinished(slot));

        var files = Season3Saves.Handler.BuildCompanionFiles(slot);
        Assert.Equal("_wd3_saveslot2_id.estore", Assert.Single(files).Name);

        var reloaded = BundleReader.Read(BundleWriter.Write(slot), slot.FileName);
        reloaded.DetectedSeasonKey = "s3";
        Season3Saves.Handler.AttachCompanionFiles(reloaded, files);
        var accessor = Season3Saves.Accessor(reloaded);
        Assert.Equal("alone", accessor.GetChoiceValue(Ending));
        Assert.All(TestSeasons.ChoicesFor("s3").Where(choice => choice.Episode is 1 or 2), choice => Assert.True(accessor.DetectCurrentChoice(choice) >= 0));

        var state = Season3Saves.Handler.GetResumeState(reloaded);
        Assert.Equal(3, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Fact]
    public void ImportingASeasonTwoSave_CopiesItsDialogNodesIntoTheBlock()
    {
        var source = Season2Saves.LoadEpisode1Save();
        var target = TestSeasons.Registry.CreateSave("s3", 1, "wd3_saveslot2.bundle");
        target.DetectedSeasonKey = "s3";
        var expected = source.EventLog!.Events.Select(entry => entry.DialogNode).OfType<ulong>().Distinct().ToList();

        Assert.True(Season3Saves.Handler.CanImportFrom(source));
        Season3Saves.Handler.ImportChoices(source, target);

        var events = target.EventLog!.Events.ToList();
        var begin = events.FindIndex(entry => entry.Has(StoryEventLog.PreviousGameBegin));
        var end = events.FindIndex(entry => entry.Has(StoryEventLog.PreviousGameEnd));
        Assert.Equal(0, begin);
        Assert.Equal(expected, events.Skip(1).Take(end - 1).Select(entry => entry.DialogNode!.Value));
        Assert.Equal(events.Count, events.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(events.Select(entry => entry.Id).Order(), events.Select(entry => entry.Id));
    }

    [Fact]
    public void ChapterList_CoversEveryEpisode()
    {
        Assert.Equal([1, 2, 3, 4, 5], S3Story.Season.Chapters.Select(episode => episode.Episode));
        foreach (var episode in S3Story.Season.Chapters)
        {
            Assert.True(episode.Chapters[0].StartsEpisode);
            Assert.Single(episode.Chapters, chapter => chapter.StartsEpisode);
            Assert.Equal(episode.Chapters.Count, episode.Chapters.Select(chapter => chapter.Id).Distinct().Count());
            Assert.All(episode.Chapters, chapter => Assert.False(string.IsNullOrWhiteSpace(chapter.Script)));
        }
    }

    [Fact]
    public void ChapterList_LeavesOutEntriesThatOnlyWorkInDeveloperBuilds()
    {
        var chapters = S3Story.Season.ChaptersOf(1)!.Chapters;

        Assert.Equal(17, chapters.Count);
        Assert.Single(chapters, chapter => chapter.Script == "JunkyardHill");
        Assert.Single(chapters, chapter => chapter.Script == "GarciaDominguezHouse");
        Assert.Equal(3, chapters.Count(chapter => chapter.Script == "VirginiaRoad"));
    }

    private static PropertySet Runtime(SaveSlot save, ulong name)
    {
        var file = save.FindFile(name);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
