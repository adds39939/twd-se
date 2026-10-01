using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Core.Tests.Unit;

public class BackupFileResolverTests
{
    private static ISeasonRegistry Registry(params ISeasonHandler[] handlers) =>
        new SeasonRegistry(handlers);

    private static SaveSlot CreateSlot(string fileName) => new()
    {
        FilePath = fileName,
        FileName = fileName,
        OuterHeader = new MetaStreamHeader { Magic = 0x4D535636 },
        FileTable = [],
    };

    [Fact]
    public void SlotBundle_BackupsOnlyItself()
    {
        var slot = CreateSlot("wd1_saveslot1.bundle");
        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new S1Handler()));

        Assert.Single(files);
        Assert.Equal("wd1_saveslot1.bundle", files[0]);
    }

    [Fact]
    public void AutosaveBundle_BackupsAutosaveAndSlot()
    {
        var slot = CreateSlot("_wd1_saveslot1_autosave.bundle");
        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new S1Handler()));

        Assert.Equal(2, files.Count);
        Assert.Equal("_wd1_saveslot1_autosave.bundle", files[0]);
        Assert.Equal("wd1_saveslot1.bundle", files[1]);
    }

    [Fact]
    public void S2AutosaveBundle_DerivesCorrectSlotName()
    {
        var slot = CreateSlot("_wd2_saveslot3_autosave.bundle");
        var files = BackupFileResolver.GetFilesToBackup(slot);

        Assert.Equal(2, files.Count);
        Assert.Equal("_wd2_saveslot3_autosave.bundle", files[0]);
        Assert.Equal("wd2_saveslot3.bundle", files[1]);
    }

    [Fact]
    public void S3Bundle_BackupsEstoreAndEpages()
    {
        var slot = CreateSlot("wd3_saveslot1.bundle");
        slot.EStorePath = "_wd3_saveslot1_id.estore";
        slot.EPagePaths = ["_wd3_saveslot1_id_Page734.epage", "_wd3_saveslot1_id_Page10249.epage"];

        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new S3Handler()));

        Assert.Equal(4, files.Count);
        Assert.Equal("wd3_saveslot1.bundle", files[0]);
        Assert.Equal("_wd3_saveslot1_id.estore", files[1]);
        Assert.Equal("_wd3_saveslot1_id_Page734.epage", files[2]);
        Assert.Equal("_wd3_saveslot1_id_Page10249.epage", files[3]);
    }

    [Fact]
    public void MichonneBundle_BackupsEstoreAndEpages()
    {
        var slot = CreateSlot("wdm_saveslot4.bundle");
        slot.EStorePath = "_wdm_saveslot4_id.estore";
        slot.EPagePaths = ["_wdm_saveslot4_id_Page971.epage"];

        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new MichonneHandler()));

        Assert.Equal(3, files.Count);
        Assert.Contains("wdm_saveslot4.bundle", files);
        Assert.Contains("_wdm_saveslot4_id.estore", files);
        Assert.Contains("_wdm_saveslot4_id_Page971.epage", files);
    }

    [Fact]
    public void S3Autosave_BackupsAutosaveAndSlot_NoEstore()
    {
        var slot = CreateSlot("_wd3_saveslot1_autosave.bundle");

        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new S3Handler()));

        Assert.Equal(2, files.Count);
        Assert.Equal("_wd3_saveslot1_autosave.bundle", files[0]);
        Assert.Equal("wd3_saveslot1.bundle", files[1]);
    }

    [Fact]
    public void S4Bundle_NoEstoreFiles()
    {
        var slot = CreateSlot("wd4_saveslot1.bundle");

        var files = BackupFileResolver.GetFilesToBackup(slot, Registry(new S4Handler()));

        Assert.Single(files);
        Assert.Equal("wd4_saveslot1.bundle", files[0]);
    }

    [Fact]
    public void NoRegistry_StillBackupsAutosaveAndSlot()
    {
        var slot = CreateSlot("_wd1_saveslot2_autosave.bundle");

        var files = BackupFileResolver.GetFilesToBackup(slot);

        Assert.Equal(2, files.Count);
        Assert.Equal("_wd1_saveslot2_autosave.bundle", files[0]);
        Assert.Equal("wd1_saveslot2.bundle", files[1]);
    }

    [Fact]
    public void NonAutosaveUnderscore_NotTreatedAsAutosave()
    {
        var slot = CreateSlot("_wd1_saveslot1_checkpoint.bundle");

        var files = BackupFileResolver.GetFilesToBackup(slot);

        Assert.Single(files);
        Assert.Equal("_wd1_saveslot1_checkpoint.bundle", files[0]);
    }
}
