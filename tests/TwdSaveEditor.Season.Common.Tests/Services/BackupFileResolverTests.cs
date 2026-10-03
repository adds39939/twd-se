using FakeItEasy;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Common.Tests.Services;

public class BackupFileResolverTests
{
    private static readonly BackupFileResolver Resolver = new(TestSeasons.Registry);

    private static SaveSlot CreateSlot(string fileName) => new()
    {
        FilePath = fileName,
        FileName = fileName,
        OuterHeader = new MetaStreamHeader { Magic = 0x4D535636 },
        Files = [],
    };

    private static BackupFileResolver WithoutSeasons()
    {
        var registry = A.Fake<ISeasonRegistry>();
        A.CallTo(() => registry.DetectFromFileName(A<string>._)).Returns(null);
        return new BackupFileResolver(registry);
    }

    [Fact]
    public void S1SlotBundle_BackupsSlotAndAutosave()
    {
        var files = Resolver.GetFilesToBackup(CreateSlot("wd1_saveslot1.bundle"));

        Assert.Equal(["wd1_saveslot1.bundle", "_wd1_saveslot1_autosave.bundle"], files);
    }

    [Fact]
    public void AutosaveBundle_BackupsAutosaveAndSlot()
    {
        var files = Resolver.GetFilesToBackup(CreateSlot("_wd1_saveslot1_autosave.bundle"));

        Assert.Equal(["_wd1_saveslot1_autosave.bundle", "wd1_saveslot1.bundle"], files);
    }

    [Fact]
    public void S2AutosaveBundle_DerivesCorrectSlotName()
    {
        var files = WithoutSeasons().GetFilesToBackup(CreateSlot("_wd2_saveslot3_autosave.bundle"));

        Assert.Equal(["_wd2_saveslot3_autosave.bundle", "wd2_saveslot3.bundle"], files);
    }

    [Fact]
    public void S3Bundle_BackupsItsEventLogAndSaves()
    {
        var slot = Season3Saves.LoadEpisode1Save();

        var files = Resolver.GetFilesToBackup(slot);

        Assert.Equal(7, files.Count);
        Assert.Equal(Season3Saves.Slot, files[0]);
        Assert.Contains(Season3Saves.Autosave, files);
        Assert.Contains(Season3Saves.Storage, files);
        Assert.All(Season3Saves.Pages, page => Assert.Contains(page, files));
    }

    [Fact]
    public void MichonneBundle_BackupsItsEventLogAndSaves()
    {
        var slot = MichonneSaves.LoadEpisode1Save();

        var files = Resolver.GetFilesToBackup(slot);

        Assert.Equal(6, files.Count);
        Assert.Equal(MichonneSaves.Slot, files[0]);
        Assert.Contains(MichonneSaves.Autosave, files);
        Assert.Contains(MichonneSaves.Checkpoint, files);
        Assert.Contains(MichonneSaves.Storage, files);
        Assert.All(MichonneSaves.Pages, page => Assert.Contains(page, files));
    }

    [Fact]
    public void S3Autosave_BackupsAutosaveAndSlot_NoEstore()
    {
        var files = Resolver.GetFilesToBackup(CreateSlot("_wd3_saveslot1_autosave.bundle"));

        Assert.Equal(["_wd3_saveslot1_autosave.bundle", "wd3_saveslot1.bundle"], files);
    }

    [Fact]
    public void S4Bundle_NoEstoreFiles()
    {
        var files = Resolver.GetFilesToBackup(CreateSlot("wd4_saveslot1.bundle"));

        Assert.Equal(["wd4_saveslot1.bundle"], files);
    }

    [Fact]
    public void UnknownSeason_StillBackupsAutosaveAndSlot()
    {
        var files = WithoutSeasons().GetFilesToBackup(CreateSlot("_wd1_saveslot2_autosave.bundle"));

        Assert.Equal(["_wd1_saveslot2_autosave.bundle", "wd1_saveslot2.bundle"], files);
    }

    [Fact]
    public void NonAutosaveUnderscore_NotTreatedAsAutosave()
    {
        var files = WithoutSeasons().GetFilesToBackup(CreateSlot("_wd1_saveslot1_checkpoint.bundle"));

        Assert.Equal(["_wd1_saveslot1_checkpoint.bundle"], files);
    }

    [Fact]
    public void CompanionHandler_AddsItsFileNamesAfterTheSlot()
    {
        var slot = CreateSlot("wd9_saveslot1.bundle");
        var handler = A.Fake<ISeasonHandler>(options => options.Implements<ICompanionFileHandler>());
        A.CallTo(() => ((ICompanionFileHandler)handler).GetCompanionFileNames(slot)).Returns(["first.estore", "second.epage"]);
        var registry = A.Fake<ISeasonRegistry>();
        A.CallTo(() => registry.DetectFromFileName(slot.FileName)).Returns(handler);

        var files = new BackupFileResolver(registry).GetFilesToBackup(slot);

        Assert.Equal(["wd9_saveslot1.bundle", "first.estore", "second.epage"], files);
    }
}
