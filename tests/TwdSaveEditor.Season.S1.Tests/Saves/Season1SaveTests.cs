using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Season.S1.Tests.Saves;

public class Season1SaveTests
{
    private const int DebugBytesPerSymbol = 4;

    [Fact]
    public void RealSave_LinksItsAutosave()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Assert.NotNull(slot.Autosave);
        Assert.False(slot.AutosaveDamaged);
        Assert.Equal(11672, slot.Autosave.Files.Count);
        Assert.Equal("WalkingDead104", slot.Autosave.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal("missingClementine", slot.Autosave.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.NotNull(slot.Autosave.FindFile(BundleFileNames.SaveGame));
    }

    [Fact]
    public void AutosaveBundle_IsNotAnEditableSaveOfItsOwn()
    {
        string[] directory = [Season1Saves.Slot, Season1Saves.Autosave, "wd1_saveslot3.bundle"];

        Assert.Equal([Season1Saves.Autosave], Season1Saves.Handler.FindCompanionFiles(Season1Saves.Slot, directory));
        Assert.Empty(Season1Saves.Handler.FindCompanionFiles(Season1Saves.Autosave, directory));
        Assert.Empty(Season1Saves.Handler.FindCompanionFiles("wd1_saveslot3.bundle", directory));
    }

    [Theory]
    [InlineData("DougCarley Saved", "carley")]
    [InlineData("ShawnDuck Choice", "duck")]
    [InlineData("Kill Duck Choice", "lee")]
    [InlineData("Weapon Choice", "inventory - spike remover")]
    [InlineData("Saved Ben", "true")]
    public void RealSave_ChoicesAreReadFromPersistentSlotValues(string choiceKey, string expected)
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Assert.Equal(expected, Season1Saves.Accessor(slot).GetChoiceValue(choiceKey));
    }

    [Fact]
    public void RealSave_EveryChoiceOfPlayedEpisodesMatchesAnOption()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        var accessor = Season1Saves.Accessor(slot);

        foreach (var choice in TestSeasons.ChoicesFor("s1").Where(c => c.Episode <= 4))
        {
            Assert.True(accessor.DetectCurrentChoice(choice) >= 0, $"{choice.ChoiceKey} has no matching option");
        }
    }

    [Fact]
    public void SetChoice_UpdatesSlotValueStatsTrackerAndAutosaveLogic()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Accessor(slot).SetChoiceValue("DougCarley Saved", "doug");

        var reloaded = Season1Saves.Reload(slot);
        Assert.Equal("doug", reloaded.Metadata!.GetString("Persistent - 101 - DougCarley Saved"));
        Assert.Equal("doug", Season1Saves.LogicGame(reloaded).GetString("DougCarley Saved"));

        var tracker = ChoicesContainer.Parse(((RawBytesValue)reloaded.Choices!.Find("Episode 101")!.Value).Data);
        Assert.Contains(("dougcarley_saved - doug", true), tracker);
        Assert.DoesNotContain(tracker, entry => entry.str == "dougcarley_saved - carley");
        Assert.Equal(tracker.Select(entry => entry.str).Order(StringComparer.Ordinal), tracker.Select(entry => entry.str));
    }

    [Fact]
    public void ClearChoice_RemovesTheSlotValueStatsTrackerEntryAndAutosaveLogic()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        var kenny = Season1Saves.Accessor(slot).GetChoiceValue("Sided With Kenny");

        Season1Saves.Accessor(slot).ClearChoiceValue("DougCarley Saved");

        var reloaded = Season1Saves.Reload(slot);
        Assert.Null(reloaded.Metadata!.GetString("Persistent - 101 - DougCarley Saved"));
        Assert.Null(Season1Saves.LogicGame(reloaded).Find("DougCarley Saved"));
        Assert.Null(Season1Saves.Accessor(reloaded).GetChoiceValue("DougCarley Saved"));
        Assert.NotNull(kenny);
        Assert.Equal(kenny, Season1Saves.Accessor(reloaded).GetChoiceValue("Sided With Kenny"));

        var tracker = ChoicesContainer.Parse(((RawBytesValue)reloaded.Choices!.Find("Episode 101")!.Value).Data);
        Assert.NotEmpty(tracker);
        Assert.DoesNotContain(tracker, entry => entry.str.StartsWith("dougcarley_saved - ", StringComparison.Ordinal));
    }

    [Fact]
    public void AutosaveThatCannotBeDecompressed_IsMarkedDamagedAndTheSlotStillLoads()
    {
        var slot = BundleReader.Read(Season1Saves.ReadBytes(Season1Saves.Slot), Season1Saves.Slot);

        Season1Saves.Handler.AttachCompanionFiles(slot, [new CompanionFile(Season1Saves.Autosave, MalformedFiles.CompressedWithOversizedPage())]);

        Assert.True(slot.AutosaveDamaged);
        Assert.Null(slot.Autosave);
        Assert.NotNull(slot.Metadata);
    }

    [Fact]
    public void SetChoice_KeepsEveryOtherAutosaveFileByteIdentical()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        var before = slot.Autosave!.Files.ToDictionary(file => file.NameSymbol, file => file.Data);

        Season1Saves.Accessor(slot).SetChoiceValue("Weapon Choice", "inventory - spanner");
        var reloaded = Season1Saves.Reload(slot);

        Assert.Equal(before.Count, reloaded.Autosave!.Files.Count);
        foreach (var file in reloaded.Autosave.Files.Where(file => file.NameSymbol != Season1Saves.LogicGameProperties))
        {
            Assert.True(before[file.NameSymbol].AsSpan().SequenceEqual(file.Data), $"file {file.NameSymbol:X16} changed");
        }

        Assert.Equal("inventory - spanner", Season1Saves.LogicGame(reloaded).GetString("Weapon Choice"));
    }

    [Fact]
    public void SetChoice_OfALaterEpisodeDoesNotTouchTheAutosave()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        var autosaveBefore = BundleWriter.Write(slot.Autosave!);

        Season1Saves.Accessor(slot).SetChoiceValue("Cut Off Arm", "false");

        Assert.Equal("false", slot.Metadata!.GetString("Persistent - 105 - Cut Off Arm"));
        Assert.Equal(autosaveBefore, BundleWriter.Write(slot.Autosave!));
    }

    [Fact]
    public void UnchangedSave_IsWrittenBackByteForByte()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        AssertSameContent(Season1Saves.ReadBytes(Season1Saves.Slot), BundleWriter.Write(slot));
        AssertSameContent(Season1Saves.ReadBytes(Season1Saves.Autosave), BundleWriter.Write(slot.Autosave!));
    }

    [Fact]
    public void EditedSave_KeepsDebugSectionsInStepWithItsSymbols()
    {
        var slot = Season1Saves.LoadEpisode4Save();
        slot.Metadata!.SetString("Slot Name", "Edited");
        Season1Saves.Accessor(slot).SetChoiceValue("Clementine Shot Lee", "true");

        var reloaded = BundleReader.Read(BundleWriter.Write(slot), slot.FileName);

        foreach (var file in reloaded.Files)
        {
            var content = MetaStreamCodec.Read(file.Data);
            Assert.Equal(PropertySetSymbols.Count(file.Properties!) * DebugBytesPerSymbol, content.Debug.Length);
            Assert.All(content.Debug, value => Assert.Equal(0, value));
        }

        Assert.Equal("Edited", reloaded.Metadata!.GetString("Slot Name"));
    }

    [Fact]
    public void ResumeState_OfRealSaveIsItsCheckpoint()
    {
        var state = Season1Saves.Handler.GetResumeState(Season1Saves.LoadEpisode4Save());

        Assert.Equal(4, state.Episode);
        Assert.Equal("missingClementine", state.Checkpoint);
        Assert.Equal("2025-09-06 14:19:47", state.SavedAt);
        Assert.False(state.StartsFromBeginning);
    }

    [Fact]
    public void DamagedAutosave_IsReportedAndRepairedByRestartingTheEpisode()
    {
        var slot = BundleReader.Read(Season1Saves.ReadBytes(Season1Saves.Slot), Season1Saves.Slot);
        var damaged = Season1Saves.ReadBytes(Season1Saves.Autosave)[..2000];

        Season1Saves.Handler.AttachCompanionFiles(slot, [new CompanionFile(Season1Saves.Autosave, damaged)]);

        Assert.Null(slot.Autosave);
        var state = Season1Saves.Handler.GetResumeState(slot);
        Assert.True(state.CheckpointDamaged);
        Assert.False(state.StartsFromBeginning);

        Season1Saves.Handler.RestartFromEpisode(slot, 4);

        state = Season1Saves.Handler.GetResumeState(slot);
        Assert.False(state.CheckpointDamaged);
        Assert.True(state.StartsFromBeginning);
        Assert.Equal([Season1Saves.Autosave], slot.ObsoleteFileNames);
    }

    [Fact]
    public void AutosaveWithMisplacedFiles_IsReportedAsDamaged()
    {
        var slot = BundleReader.Read(Season1Saves.ReadBytes(Season1Saves.Slot), Season1Saves.Slot);
        var autosave = BundleReader.Read(Season1Saves.ReadBytes(Season1Saves.Autosave), Season1Saves.Autosave);
        autosave.Files[5].Data = autosave.Files[5].Data[2..];

        Season1Saves.Handler.AttachCompanionFiles(slot, [new CompanionFile(Season1Saves.Autosave, BundleWriter.Write(autosave))]);

        Assert.True(slot.AutosaveDamaged);
        Assert.True(Season1Saves.Handler.GetResumeState(slot).CheckpointDamaged);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void RestartFromEpisode_MakesTheGameStartThatEpisodeFresh(int episode)
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromEpisode(slot, episode);

        var reloaded = Season1Saves.Reload(slot);
        var metadata = reloaded.Metadata!;
        Assert.Equal(episode, metadata.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal($"WalkingDead10{episode}", metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(string.Empty, metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(0, metadata.GetInt(SlotMetadataKeys.LatestSerial));
        for (var number = 1; number <= 6; number++)
        {
            Assert.Equal(number < episode, metadata.GetBool(SlotMetadataKeys.CompletedEpisode(number)));
        }

        Assert.Null(reloaded.Autosave);
        Assert.Equal([Season1Saves.Autosave], slot.ObsoleteFileNames);
        Assert.Empty(Season1Saves.Handler.BuildCompanionFiles(slot));

        var state = Season1Saves.Handler.GetResumeState(reloaded);
        Assert.Equal(episode, state.Episode);
        Assert.True(state.StartsFromBeginning);
    }

    [Fact]
    public void RestartFromEpisode_FillsEveryDecisionTheGameChecksBeforeStarting()
    {
        var slot = Season1Saves.Handler.CreateBlankSave("wd1_saveslot3.bundle", "WalkingDead101");

        Season1Saves.Handler.RestartFromEpisode(slot, 5);

        var accessor = Season1Saves.Accessor(slot);
        foreach (var choice in TestSeasons.ChoicesFor("s1"))
        {
            Assert.Equal(choice.Episode < 5, accessor.GetChoiceValue(choice.ChoiceKey) != null);
        }
    }

    [Fact]
    public void RestartFromEpisode_KeepsDecisionsThatWereAlreadyMade()
    {
        var slot = Season1Saves.LoadEpisode4Save();

        Season1Saves.Handler.RestartFromEpisode(slot, 5);

        Assert.Equal("carley", Season1Saves.Accessor(slot).GetChoiceValue("DougCarley Saved"));
        Assert.Equal("inventory - spike remover", Season1Saves.Accessor(slot).GetChoiceValue("Weapon Choice"));
    }

    [Fact]
    public void RestartFrom400Days_RequiresAllFiveEpisodesOfDecisions()
    {
        var slot = TestSeasons.Registry.CreateSave("s1_400days", 1, "wd1_saveslot4.bundle");

        Assert.Equal(6, slot.Metadata!.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal("WalkingDead106", slot.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));

        var accessor = Season1Saves.Accessor(slot);
        Assert.All(TestSeasons.ChoicesFor("s1"), choice => Assert.NotNull(accessor.GetChoiceValue(choice.ChoiceKey)));
        Assert.All(TestSeasons.ChoicesFor("s1_400days"), choice => Assert.Null(accessor.GetChoiceValue(choice.ChoiceKey)));
    }

    [Fact]
    public void NewSave_HasTheSameShapeAsARealSlotBundle()
    {
        var real = Season1Saves.LoadEpisode4Save();
        var created = BundleReader.Read(
            BundleWriter.Write(TestSeasons.Registry.CreateSave("s1", 4, "wd1_saveslot3.bundle")), "wd1_saveslot3.bundle");

        Assert.Equal(real.Files.Select(file => file.Name), created.Files.Select(file => file.Name));
        Assert.Equal(real.Files.Select(file => file.NameSymbol), created.Files.Select(file => file.NameSymbol));
        Assert.Equal(real.Files.Select(file => file.TypeSymbol), created.Files.Select(file => file.TypeSymbol));
        Assert.Equal(real.Metadata!.ParentSymbols, created.Metadata!.ParentSymbols);
        Assert.Equal(real.Metadata.Flags, created.Metadata.Flags);
        Assert.Equal(real.Choices!.Flags, created.Choices!.Flags);

        var realKeys = real.Metadata.AllProperties.Select(property => property.KeySymbol).ToHashSet();
        var createdKeys = created.Metadata.AllProperties.Select(property => property.KeySymbol).ToList();
        Assert.All(createdKeys, key => Assert.Contains(key, realKeys));

        for (var i = 0; i < created.Files.Count; i++)
        {
            var realHeader = MetaStreamCodec.Read(real.Files[i].Data).Header;
            var createdContent = MetaStreamCodec.Read(created.Files[i].Data);
            Assert.Equal(realHeader.VersionEntries.Select(entry => (entry.TypeCrc, entry.VersionCrc)),
                createdContent.Header.VersionEntries.Select(entry => (entry.TypeCrc, entry.VersionCrc)));
            Assert.Equal(PropertySetSymbols.Count(created.Files[i].Properties!) * DebugBytesPerSymbol, createdContent.Debug.Length);
        }

        var outer = MetaStreamCodec.Read(BundleWriter.Write(created));
        Assert.Equal(8 + 40 * created.Files.Count, outer.Default.Length);
        Assert.Equal(8 * created.Files.Count, outer.Debug.Length);
    }

    [Fact]
    public void NewSave_TypeGroupsAndKeysAreOrderedLikeTheGameWritesThem()
    {
        var created = TestSeasons.Registry.CreateSave("s1", 5, "wd1_saveslot3.bundle");

        foreach (var properties in new[] { created.Metadata!, created.Choices! })
        {
            Assert.Equal(properties.TypeGroups.Select(group => group.TypeSymbol.Value).Order(), properties.TypeGroups.Select(group => group.TypeSymbol.Value));
            foreach (var group in properties.TypeGroups)
            {
                Assert.Equal(group.Properties.Select(p => p.KeySymbol.Value).Order(), group.Properties.Select(p => p.KeySymbol.Value));
            }
        }
    }

    [Fact]
    public void NewSave_HasNoAutosaveAndRemovesAStaleOne()
    {
        var created = TestSeasons.Registry.CreateSave("s1", 2, "wd1_saveslot3.bundle");

        Assert.Null(created.Autosave);
        Assert.Equal(string.Empty, created.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(["_wd1_saveslot3_autosave.bundle"], created.ObsoleteFileNames);
    }

    private static void AssertSameContent(byte[] original, byte[] rewritten)
    {
        var before = MetaStreamCodec.Read(original);
        var after = MetaStreamCodec.Read(rewritten);

        Assert.Equal(before.Header.IsDefaultCompressed, after.Header.IsDefaultCompressed);
        Assert.Equal(before.Header.IsDebugCompressed, after.Header.IsDebugCompressed);
        Assert.Equal(before.Header.IsAsyncCompressed, after.Header.IsAsyncCompressed);
        Assert.True(before.Default.AsSpan().SequenceEqual(after.Default));
        Assert.True(before.Debug.AsSpan().SequenceEqual(after.Debug));
        Assert.True(before.Async.AsSpan().SequenceEqual(after.Async));
    }
}
