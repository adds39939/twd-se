using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S2.Saves;
using TwdSaveEditor.Season.S2.Decisions;
using TwdSaveEditor.Season.S2.Chapters;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.S2.Tests.Saves;

public class Season2SaveTests
{
    private const string Rescue = "Episode 201 - Rescue Choice";
    private const string Dog = "Episode 201 - Killed Dog";
    private const string Christa = "Episode 201 - Helped Christa";
    private const string FirstCheckpoint = "_wd2_saveslot1_checkpoint1.bundle";

    [Fact]
    public void EventLogFiles_AreRewrittenByteForByte()
    {
        var storage = Season2Saves.ReadBytes(Season2Saves.Storage);
        Assert.Equal(storage, EventLogCodec.WriteStorage(EventLogCodec.ReadStorage(storage)));

        foreach (var name in Season2Saves.Pages)
        {
            var page = Season2Saves.ReadBytes(name);
            Assert.Equal(page, EventLogCodec.WritePage(EventLogCodec.ReadPage(page)));
        }
    }

    [Fact]
    public void RealSave_LoadsItsEventLogAndSaves()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Assert.NotNull(slot.EventLog);
        Assert.Equal(3, slot.EventLog.PageFiles.Count);
        Assert.Equal(4, slot.EventLog.Pages.Count());
        Assert.Equal(2553, slot.EventLog.Events.Count());
        Assert.Equal(3202u, slot.EventLog.Storage.LastEventId);
        Assert.Equal("WalkingDead201", Assert.Single(slot.Checkpoints).Metadata!.GetString(SaveMetadataKeys.Episode));
    }

    [Fact]
    public void CompanionFiles_AreTheEventLogAndEverySaveOfTheSlot()
    {
        string[] directory =
        [
            Season2Saves.Slot, "wd2_saveslot2.bundle", "_wd2_saveslot2_autosave.bundle", "_wd2_saveSlot1_id_Page1893.epage",
            "_wd2_saveslot1_checkpoint3.bundle", "_wd2_saveslot10_checkpoint1.bundle", .. Season2Saves.CompanionNames,
        ];

        var found = Season2Saves.Handler.FindCompanionFiles(Season2Saves.Slot, directory);

        Assert.Equal(7, found.Count);
        Assert.Contains("_wd2_saveslot1_checkpoint3.bundle", found);
        Assert.Contains("_wd2_saveSlot1_id_Page1893.epage", found);
        Assert.DoesNotContain("_wd2_saveslot2_autosave.bundle", found);
        Assert.DoesNotContain("_wd2_saveslot10_checkpoint1.bundle", found);
        Assert.Empty(Season2Saves.Handler.FindCompanionFiles(Season2Saves.Autosave, directory));
    }

    [Theory]
    [InlineData(Dog, "killed_the_dog")]
    [InlineData("Episode 201 - Gave Victor Water", "true")]
    [InlineData("Episode 201 - Staying Choice", "leave")]
    [InlineData("Episode 201 - Stole Watch", "true")]
    [InlineData(Rescue, null)]
    [InlineData("Episode 204 - Gave Rebecca Pills", "false")]
    public void RealSave_DecisionsAreReadFromTheEventLog(string choiceKey, string? expected)
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Assert.Equal(expected, Season2Saves.Accessor(slot).GetChoiceValue(choiceKey));
    }

    [Fact]
    public void Catalog_EveryChoiceHasNodesForItsOptions()
    {
        var choices = TestSeasons.ChoicesFor("s2").ToList();

        Assert.Equal(40, choices.Count);
        foreach (var choice in choices)
        {
            var decision = S2DecisionCatalog.Find(choice.ChoiceKey);
            Assert.NotNull(decision);
            Assert.Equal(choice.Episode, decision.Episode);
            Assert.Equal(choice.Options.Select(option => option.Value), decision.Options.Select(option => option.Value));
            Assert.True(decision.Options.Count(option => option.Nodes.Count == 0) <= 1);
        }
    }

    [Fact]
    public void UnchangedSave_WritesNoCompanionFiles()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Assert.Empty(Season2Saves.Handler.BuildCompanionFiles(slot));
    }

    [Fact]
    public void ChangingADecision_SwapsItsNodeInPlace()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var before = slot.EventLog!.Events.Select(entry => entry.Id).ToList();

        Season2Saves.Accessor(slot).SetChoiceValue(Dog, "walked_away_from_the_dog");

        var reloaded = Season2Saves.Reload(slot);
        Assert.Equal("walked_away_from_the_dog", Season2Saves.Accessor(reloaded).GetChoiceValue(Dog));
        Assert.Equal(before, reloaded.EventLog!.Events.Select(entry => entry.Id));
        Assert.Single(Season2Saves.Handler.BuildCompanionFiles(slot));
    }

    [Fact]
    public void SettingADecisionThatWasNeverMade_AddsItBeforeTheLatestSave()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var count = slot.EventLog!.Events.Count();

        Season2Saves.Accessor(slot).SetChoiceValue(Rescue, "nick");

        var events = Season2Saves.Reload(slot).EventLog!.Events.ToList();
        var node = S2EventLogEditor.NodeSymbol(S2DecisionCatalog.Find(Rescue)!.Find("nick")!.Nodes[0]);
        var added = events.FindIndex(entry => entry.DialogNode == node);
        var latest = events.FindIndex(entry => entry.SaveSerial == 16);

        Assert.Equal(count + 1, events.Count);
        Assert.Equal(latest - 1, added);
        Assert.True(events[added].Id < events[latest].Id);
        Assert.Equal(events.Count, events.Select(entry => entry.Id).Distinct().Count());
    }

    [Fact]
    public void ChoosingAnOptionWithoutANode_RemovesTheOthers()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var accessor = Season2Saves.Accessor(slot);
        accessor.SetChoiceValue(Christa, "true");
        var count = slot.EventLog!.Events.Count();

        accessor.SetChoiceValue("Episode 203 - Shot Carver", "true");
        accessor.SetChoiceValue("Episode 203 - Shot Carver", "false");

        Assert.Equal("false", accessor.GetChoiceValue("Episode 203 - Shot Carver"));
        Assert.Equal("true", accessor.GetChoiceValue(Christa));
        Assert.Equal(count, slot.EventLog.Events.Count());
    }

    [Fact]
    public void DecisionWithARequiredNode_GetsThatNodeToo()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var decision = S2DecisionCatalog.Find("Episode 203 - Bonnie Truth")!;

        Season2Saves.Accessor(slot).SetChoiceValue(decision.ChoiceKey, "chose_to_hide_luke_s");

        var nodes = slot.EventLog!.Events.Select(entry => entry.DialogNode).ToHashSet();
        Assert.Contains(S2EventLogEditor.NodeSymbol(Assert.Single(decision.Requires)), nodes);
        Assert.Equal("chose_to_hide_luke_s", Season2Saves.Accessor(slot).GetChoiceValue(decision.ChoiceKey));
    }

    [Fact]
    public void ChangingADecision_UpdatesTheFlagInSavesThatAlreadyHoldIt()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var save = Assert.Single(slot.Checkpoints);
        var logic = save.FindFile(S2SlotFiles.LogicGameProperties)!;
        Assert.True(TwdSaveEditor.Core.Binary.Bundles.BundleReader.TryParseProperties(logic));
        logic.Properties!.SetString(Rescue, "Pete");
        logic.Properties.SetBool(Christa, false);

        var accessor = Season2Saves.Accessor(slot);
        accessor.SetChoiceValue(Rescue, "nick");
        accessor.SetChoiceValue(Christa, "true");

        Assert.Equal("Nick", logic.Properties.GetString(Rescue));
        Assert.True(logic.Properties.GetBool(Christa));
        Assert.Contains(Season2Saves.Autosave, Season2Saves.Handler.BuildCompanionFiles(slot).Select(file => file.Name));
    }

    [Fact]
    public void RestartFromNextEpisode_KeepsTheSavesAndFillsEveryEarlierDecision()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Season2Saves.Handler.RestartFromEpisode(slot, 2);

        var reloaded = Season2Saves.Reload(slot);
        var accessor = Season2Saves.Accessor(reloaded);
        var state = Season2Saves.Handler.GetResumeState(reloaded);

        Assert.Equal("WalkingDead202", reloaded.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(Season2Saves.Autosave, reloaded.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(16, reloaded.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
        Assert.Equal(2, state.Episode);
        Assert.True(state.StartsFromBeginning);
        Assert.Empty(slot.ObsoleteFileNames);
        Assert.Equal("killed_the_dog", accessor.GetChoiceValue(Dog));
        Assert.Equal("pete", accessor.GetChoiceValue(Rescue));
        Assert.Equal("true", accessor.GetChoiceValue(Christa));
        Assert.All(TestSeasons.ChoicesFor("s2", 1), choice => Assert.True(accessor.DetectCurrentChoice(choice) >= 0));
        Assert.Equal(16, reloaded.EventLog!.Events.Last().SaveSerial);
    }

    [Fact]
    public void RestartFromTheFirstEpisode_ReplacesEverySaveWithAnOpeningCheckpoint()
    {
        var slot = Season2Saves.LoadEpisode1Save();

        Season2Saves.Handler.RestartFromEpisode(slot, 1);

        var checkpoint = Assert.Single(slot.Checkpoints);
        Assert.Equal(FirstCheckpoint, checkpoint.FileName);
        Assert.True(checkpoint.Modified);
        Assert.Equal("WalkingDead201", checkpoint.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal("chapter1", checkpoint.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal("PreviouslyOn.lua", SaveGameCodec.Read(checkpoint.FindFile(BundleFileNames.SaveGame)!.Data).LuaDoFile);
        Assert.Null(checkpoint.FindFile(S2SlotFiles.ScriptProperties));
        Assert.Null(checkpoint.FindFile(S2SlotFiles.LogicGameProperties));

        Assert.Equal([Season2Saves.Autosave, .. Season2Saves.Pages.Order(StringComparer.OrdinalIgnoreCase)], slot.ObsoleteFileNames.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(FirstCheckpoint, slot.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(1, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
        Assert.Equal(1, Assert.Single(slot.EventLog!.Events).SaveSerial);
        Assert.Empty(slot.EventLog.Storage.Pages);

        var storage = EventLogCodec.ReadStorage(Season2Saves.Handler.BuildCompanionFiles(slot).Single(file => file.Name == Season2Saves.Storage).Data);
        Assert.Equal(1u, storage.LastEventId);

        var state = Season2Saves.Handler.GetResumeState(Season2Saves.Reload(slot));
        Assert.Equal(1, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Theory]
    [InlineData("EpisodeCompleteShowEpisode2", 2, false)]
    [InlineData("ShowEndCredits2", 3, false)]
    [InlineData("ShowEndCredits5", 5, true)]
    [InlineData("finished", 5, true)]
    public void ResumeState_OfASaveBetweenEpisodesIsTheNextEpisodeOrTheFinishedSeason(string progress, int episode, bool finished)
    {
        var slot = Season2Saves.LoadEpisode1Save();
        slot.Metadata!.SetString(SlotMetadataKeys.EpisodeInProgress, progress);

        var state = Season2Saves.Handler.GetResumeState(slot);

        Assert.Equal(episode, state.Episode);
        Assert.Null(state.Checkpoint);
        Assert.Equal(finished, state.SeasonFinished);
        Assert.Equal(!finished, state.StartsFromBeginning);
    }

    [Fact]
    public void RestartingAFinishedSeason_PutsAnEpisodeBackInProgress()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        slot.Metadata!.SetString(SlotMetadataKeys.EpisodeInProgress, "finished");

        Season2Saves.Handler.RestartFromEpisode(slot, 2);

        var state = Season2Saves.Handler.GetResumeState(slot);
        Assert.Equal("WalkingDead202", slot.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(2, state.Episode);
        Assert.False(state.SeasonFinished);
    }

    [Fact]
    public void ResumeFromAChapter_WritesTheDeveloperMenuStateForThatScene()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        Season2Saves.Accessor(slot).SetChoiceValue(Dog, "walked_away_from_the_dog");

        Season2Saves.Handler.RestartFromChapter(slot, 1, "CabinShedII");

        var checkpoint = Assert.Single(slot.Checkpoints);
        Assert.Equal("chapter9", checkpoint.Metadata!.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal("CabinShedInterior", checkpoint.Metadata.GetString(S2CheckpointBuilder.SavedScript));

        var save = SaveGameCodec.Read(checkpoint.FindFile(BundleFileNames.SaveGame)!.Data);
        Assert.Equal("CabinShedInterior.lua", save.LuaDoFile);
        Assert.Empty(save.Agents);
        Assert.Equal(checkpoint.Files.Skip(2).Select(file => file.NameSymbol), save.RuntimePropertyNames);

        var reloaded = Season2Saves.Reload(slot);
        var written = Assert.Single(reloaded.Checkpoints);
        Assert.Equal("DebugMenu", Runtime(written, S2SlotFiles.ScriptProperties).GetString(S2CheckpointBuilder.PreviousScript));
        Assert.Equal("chapter9", Runtime(written, S2SlotFiles.SaveLoadProperties).GetString(S2CheckpointBuilder.ChapterId));

        var logic = Runtime(written, S2SlotFiles.LogicGameProperties);
        Assert.Equal(3, logic.GetInt("Act"));
        Assert.Equal(slot.Choices!.GetString("DougCarley Saved"), logic.GetString("Episode 101 - DougCarley Saved"));
        Assert.Equal(bool.Parse(slot.Choices.GetString("Shot Dan")!), logic.GetBool("Episode 106 - Shot Dan"));
        Assert.Null(logic.Find(Rescue));

        var accessor = Season2Saves.Accessor(reloaded);
        Assert.Equal("walked_away_from_the_dog", accessor.GetChoiceValue(Dog));
        Assert.Equal("true", accessor.GetChoiceValue(Christa));
        Assert.Null(new S2EventLogEditor(reloaded).FindSeen(S2DecisionCatalog.Find(Rescue)!));
        Assert.Equal(1, reloaded.EventLog!.Events.Last().SaveSerial);

        var state = Season2Saves.Handler.GetResumeState(reloaded);
        Assert.Equal(1, state.Episode);
        Assert.Equal("Cabin Shed II", state.Checkpoint);
    }

    [Fact]
    public void ResumeFromAChapterOfTheNextEpisode_KeepsEarlierSavesAndCarriesTheirDecisions()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        Season2Saves.Accessor(slot).SetChoiceValue(Rescue, "nick");

        Season2Saves.Handler.RestartFromChapter(slot, 2, "LodgeMainDinner");

        Assert.Equal([Season2Saves.Autosave, FirstCheckpoint], slot.Checkpoints.Select(save => save.FileName));
        Assert.Empty(slot.ObsoleteFileNames);
        Assert.Equal("WalkingDead202", slot.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(FirstCheckpoint, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(17, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));

        var reloaded = Season2Saves.Reload(slot);
        var checkpoint = reloaded.Checkpoints.Single(save => save.FileName == FirstCheckpoint);
        Assert.Equal(17, checkpoint.Metadata!.GetInt(SaveMetadataKeys.Serial));
        Assert.Equal("202_chapter9", checkpoint.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal("LodgeMainRoom.lua", SaveGameCodec.Read(checkpoint.FindFile(BundleFileNames.SaveGame)!.Data).LuaDoFile);

        var logic = Runtime(checkpoint, S2SlotFiles.LogicGameProperties);
        Assert.Equal("Nick", logic.GetString(Rescue));
        Assert.Equal(true, logic.GetBool(Christa));
        Assert.Equal(3, logic.GetInt("Act"));
        Assert.Equal(true, logic.GetBool("3Lodge - Topped Tree"));

        Assert.Equal(17, reloaded.EventLog!.Events.Last().SaveSerial);
        Assert.Equal("nick", Season2Saves.Accessor(reloaded).GetChoiceValue(Rescue));
        Assert.Equal("Lodge Main Dinner", Season2Saves.Handler.GetResumeState(reloaded).Checkpoint);
    }

    [Fact]
    public void NewSave_StartsWithTheSeasonTwoLayoutAndAnOpeningCheckpoint()
    {
        var slot = TestSeasons.Registry.CreateSave("s2", 3, Season2Saves.Slot);

        Assert.Equal("WalkingDead203", slot.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal([Symbol.FromString("metadata_slot_s2.prop")], slot.Metadata.ParentSymbols);
        Assert.All(TestSeasons.ChoicesFor("s1"), choice => Assert.Equal(choice.Options[0].Value.ToLowerInvariant(), slot.Choices!.GetString(choice.ChoiceKey)));
        Assert.NotNull(slot.Choices!.Find("ChoiceTracker - 106"));

        var checkpoint = Assert.Single(slot.Checkpoints);
        Assert.Equal("WalkingDead203", checkpoint.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal("203_chapter1", checkpoint.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal("PreviouslyOn.lua", SaveGameCodec.Read(checkpoint.FindFile(BundleFileNames.SaveGame)!.Data).LuaDoFile);
        Assert.Equal(checkpoint.FileName, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));

        var accessor = Season2Saves.Accessor(slot);
        Assert.All(TestSeasons.ChoicesFor("s2").Where(choice => choice.Episode < 3), choice => Assert.Equal(0, accessor.DetectCurrentChoice(choice)));
        Assert.Equal(1, slot.EventLog!.Events.Last().SaveSerial);
        Assert.Equal([checkpoint.FileName, Season2Saves.Storage], Season2Saves.Handler.BuildCompanionFiles(slot).Select(file => file.Name).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChapterList_CoversEveryEpisodeWithUsableEntries()
    {
        var decisions = S2DecisionCatalog.All.Select(decision => decision.ChoiceKey).ToHashSet();
        for (var episode = S2ResumePoint.FirstEpisode; episode <= S2ResumePoint.LastEpisode; episode++)
        {
            var chapters = S2ChapterCatalog.ForEpisode(episode)!;
            Assert.Equal("PreviouslyOn", chapters.Opening.Script);
            Assert.Same(chapters.Chapters[0], chapters.Opening);
            Assert.Equal(chapters.Chapters.Count, chapters.Chapters.Select(chapter => chapter.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(chapters.Chapters, chapter =>
            {
                Assert.NotEmpty(chapter.ChapterId);
                Assert.NotEmpty(chapter.Group);
                Assert.Subset(decisions, chapter.Decided.ToHashSet());
                Assert.All(chapter.Decided, key => Assert.Equal(episode, S2DecisionCatalog.Find(key)!.Episode));
            });
            Assert.Equal(chapters.Chapters.Select(chapter => chapter.Id), Season2Saves.Handler.GetChapters(episode).Select(chapter => chapter.Id));
        }

        Assert.Equal(35, S2ChapterCatalog.ImportedKeys.Sum(entry => entry.Keys.Count));
    }

    private static PropertySet Runtime(SaveSlot save, ulong name)
    {
        var file = save.FindFile(name);
        Assert.NotNull(file);
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }

    [Fact]
    public void TruncatingInsideAFlushedPage_TurnsItIntoTheCurrentPage()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        slot.Checkpoints[0].Metadata!.SetInt(SaveMetadataKeys.Serial, 9);

        new S2EventLogEditor(slot).TruncateAfterSerial(9);

        var log = slot.EventLog!;
        Assert.Empty(log.PageFiles);
        Assert.Empty(log.Storage.Pages);
        Assert.Equal(9, log.Storage.CurrentPage!.Events.Last().SaveSerial);
        Assert.Equal(log.Storage.CurrentPage.Events.Max(entry => entry.Id), log.Storage.LastEventId);
        Assert.Equal(3, slot.ObsoleteFileNames.Count);
    }

    [Fact]
    public void EditedEventLog_IsStillReadByTheCodecAfterWriting()
    {
        var slot = Season2Saves.LoadEpisode1Save();
        var accessor = Season2Saves.Accessor(slot);
        foreach (var choice in TestSeasons.ChoicesFor("s2"))
            accessor.ApplyChoice(choice, choice.Options.Length - 1);

        var reloaded = Season2Saves.Reload(slot);
        foreach (var choice in TestSeasons.ChoicesFor("s2"))
            Assert.Equal(choice.Options.Length - 1, Season2Saves.Accessor(reloaded).DetectCurrentChoice(choice));
    }
}
