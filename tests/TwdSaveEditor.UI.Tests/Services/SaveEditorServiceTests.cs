using System.IO.Compression;
using FakeItEasy;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.UI.Model;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Tests.Services;

public class SaveEditorServiceTests
{
    private readonly IFileSystemService _fs = A.Fake<IFileSystemService>();
    private readonly ISeasonRegistry _registry = A.Fake<ISeasonRegistry>();
    private readonly IBackupFileResolver _backupFiles = A.Fake<IBackupFileResolver>();
    private const string FolderName = "The Walking Dead Definitive";

    public SaveEditorServiceTests()
    {
        A.CallTo(() => _registry.DetectFromFileName(A<string>._)).Returns(null);
        A.CallTo(() => _backupFiles.GetFilesToBackup(A<SaveSlot>._)).ReturnsLazily((SaveSlot slot) => [slot.FileName]);
        A.CallTo(() => _fs.BackupFiles(A<string>._, A<string[]>._))
            .ReturnsLazily((string folder, string[] _) => new BackupResult(folder, null));
        A.CallTo(() => _fs.WriteFile(A<string>._, A<byte[]>._)).Returns(true);
        A.CallTo(() => _fs.ListFiles(A<string>._)).Returns(Array.Empty<string>());
        A.CallTo(() => _fs.PickDirectory()).Returns(true);
        A.CallTo(() => _fs.GetDirectoryName()).Returns(FolderName);
    }

    private static SaveSlot CreateSlot(string fileName) => new()
    {
        FilePath = fileName,
        FileName = fileName,
        OuterHeader = new MetaStreamHeader(),
        Files = [],
    };

    private SaveEditorService Service(ISeasonRegistry? registry = null) =>
        new(_fs, registry ?? _registry, A.Fake<ISaveBundleSerializer>(), new SaveBackupService(_fs, _backupFiles));

    private SaveEditorService ServiceWith(params SaveSlot[] saves)
    {
        var service = Service();
        service.Saves.AddRange(saves);
        return service;
    }

    private async Task<SaveEditorService> InFolder(params SaveSlot[] saves)
    {
        var service = ServiceWith(saves);
        await service.PickDirectory();
        return service;
    }

    private SaveEditorService ServiceReadingSaves() =>
        new(_fs, TestSeasons.Registry, new SaveBundleSerializer(), new SaveBackupService(_fs, _backupFiles));

    [Fact]
    public void MarkModified_MarksOnlyThatSave()
    {
        var first = CreateSlot("wd2_saveslot1.bundle");
        var second = CreateSlot("wd3_saveslot1.bundle");
        var service = ServiceWith(first, second);

        service.MarkModified(first);

        Assert.True(service.IsModified(first));
        Assert.False(service.IsModified(second));
        Assert.True(service.HasUnsavedChanges);
        Assert.Equal([first], service.ModifiedSaves);
    }

    [Fact]
    public void MarkModified_AdvancesTheRevisionOfThatSaveOnly()
    {
        var first = CreateSlot("wd2_saveslot1.bundle");
        var second = CreateSlot("wd3_saveslot1.bundle");
        var service = ServiceWith(first, second);

        service.MarkModified(first);
        service.MarkModified(first);

        Assert.Equal(2, service.Revision(first));
        Assert.Equal(0, service.Revision(second));
    }

    [Fact]
    public async Task SaveFile_ClearsOnlyTheSaveThatWasWritten()
    {
        var first = CreateSlot("wd2_saveslot1.bundle");
        var second = CreateSlot("wd3_saveslot1.bundle");
        var service = await InFolder(first, second);
        service.MarkModified(first);
        service.MarkModified(second);

        await service.SaveFile(second);

        Assert.True(service.IsModified(first));
        Assert.False(service.IsModified(second));
        Assert.Equal([first], service.ModifiedSaves);
    }

    [Fact]
    public async Task SaveFile_WritesAndDeletesNothingWhenTheBackupFails()
    {
        var slot = CreateSlot("wd2_saveslot1.bundle");
        slot.ObsoleteFileNames.Add("_wd2_saveslot1_id_Page913.epage");
        var service = await InFolder(slot);
        service.MarkModified(slot);
        A.CallTo(() => _fs.BackupFiles(A<string>._, A<string[]>._))
            .Returns(new BackupResult(null, "wd2_saveslot1.bundle: The request is not allowed."));

        await service.SaveFile(slot);

        A.CallTo(() => _fs.WriteFile(A<string>._, A<byte[]>._)).MustNotHaveHappened();
        A.CallTo(() => _fs.DeleteFile(A<string>._)).MustNotHaveHappened();
        Assert.True(service.IsModified(slot));
        Assert.Contains("wd2_saveslot1.bundle", service.StatusMessage);
    }

    [Fact]
    public async Task SaveFile_WithoutAFolder_DownloadsTheChangedFilesAndTheFilesToDelete()
    {
        var slot = CreateSlot("wd2_saveslot1.bundle");
        slot.ObsoleteFileNames.Add("_wd2_saveslot1_id_Page913.epage");
        var service = ServiceWith(slot);
        service.MarkModified(slot);
        byte[]? archive = null;
        A.CallTo(() => _fs.DownloadFile("wd2_saveslot1.zip", A<byte[]>._)).Invokes((string _, byte[] data) => archive = data);

        await service.SaveFile(slot);

        using var zip = new ZipArchive(new MemoryStream(archive!));
        Assert.Equal(["wd2_saveslot1.bundle", SaveArchive.DeleteListName], zip.Entries.Select(entry => entry.FullName));
        using var deleteList = new StreamReader(zip.GetEntry(SaveArchive.DeleteListName)!.Open());
        Assert.Contains("_wd2_saveslot1_id_Page913.epage", deleteList.ReadToEnd());
        A.CallTo(() => _fs.WriteFile(A<string>._, A<byte[]>._)).MustNotHaveHappened();
        A.CallTo(() => _fs.DeleteFile(A<string>._)).MustNotHaveHappened();
        Assert.False(service.IsModified(slot));
        Assert.Empty(slot.ObsoleteFileNames);
    }

    [Fact]
    public async Task DiscardChanges_ReloadsTheSaveFromItsFiles()
    {
        var service = ServiceReadingSaves();
        await service.LoadFiles(Season2Saves.CompanionNames.Prepend(Season2Saves.Slot).ToDictionary(name => name, Season2Saves.ReadBytes));
        var original = service.SelectedSave = service.Saves.Single();
        var accessor = service.GetChoiceAccessor(original)!;
        var choice = Season2Saves.Handler.Choices.First(choice => accessor.GetChoiceValue(choice.ChoiceKey) != null);
        var before = accessor.GetChoiceValue(choice.ChoiceKey);
        accessor.ApplyChoice(choice, Array.FindIndex(choice.Options, option => option.Value != before));
        service.MarkModified(original);

        await service.DiscardChanges(original);

        var reloaded = service.Saves.Single();
        Assert.NotSame(original, reloaded);
        Assert.Same(reloaded, service.SelectedSave);
        Assert.False(service.HasUnsavedChanges);
        Assert.Equal(before, service.GetChoiceAccessor(reloaded)!.GetChoiceValue(choice.ChoiceKey));
    }

    [Fact]
    public async Task ReloadDirectory_ReadsTheFolderAgainAndKeepsTheSelectedSave()
    {
        A.CallTo(() => _fs.ListFiles(A<string>._)).Returns([Season1Saves.Slot, Season1Saves.Autosave]);
        A.CallTo(() => _fs.ReadFile(A<string>._)).ReturnsLazily((string name) => Season1Saves.ReadBytes(name));
        var service = ServiceReadingSaves();
        await service.PickDirectory();
        await service.LoadDirectory();
        var before = service.SelectedSave = service.Saves.Single();

        await service.ReloadDirectory();

        Assert.NotSame(before, service.SelectedSave);
        Assert.Equal(Season1Saves.Slot, service.SelectedSave!.FileName);
    }

    [Fact]
    public async Task RestoreDirectory_LoadsARememberedFolderThatStillHasAccess()
    {
        A.CallTo(() => _fs.RememberedDirectory()).Returns(new RememberedDirectory(FolderName, true));
        var service = Service();

        await service.RestoreDirectory();

        Assert.Equal(FolderName, service.DirectoryName);
        Assert.False(service.DownloadsChanges);
        A.CallTo(() => _fs.ListFiles(A<string>._)).MustHaveHappened();
    }

    [Fact]
    public async Task RestoreDirectory_OffersToReopenAFolderThatNeedsPermission()
    {
        A.CallTo(() => _fs.RememberedDirectory()).Returns(new RememberedDirectory(FolderName, false));
        var service = Service();

        await service.RestoreDirectory();

        Assert.Null(service.DirectoryName);
        Assert.Equal(FolderName, service.RememberedDirectoryName);
        A.CallTo(() => _fs.ListFiles(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task LoadDirectory_ForgetsTheUnsavedChangesOfTheSavesItReplaces()
    {
        var slot = CreateSlot("wd2_saveslot1.bundle");
        var service = ServiceWith(slot);
        service.MarkModified(slot);

        await service.LoadDirectory();

        Assert.False(service.HasUnsavedChanges);
        Assert.Empty(service.ModifiedSaves);
    }

    [Fact]
    public void CascadeChoice_ClearsTheDecisionInLaterSeasons()
    {
        var season2 = Season2Saves.LoadEpisode1Save();
        var accessor = Season2Saves.Accessor(season2);
        var choice = TestSeasons.ChoicesFor("s1").First(choice => accessor.GetChoiceValue(choice.ChoiceKey) != null);
        var service = Service(TestSeasons.Registry);
        service.Saves.Add(season2);
        service.CascadeChoices = true;

        service.CascadeChoice(choice.ChoiceKey, null, "s1");

        Assert.Null(accessor.GetChoiceValue(choice.ChoiceKey));
        Assert.True(service.IsModified(season2));
    }

    [Fact]
    public void CascadeChoice_MarksTheSavesItChanges()
    {
        var season2 = Season2Saves.LoadEpisode1Save();
        var accessor = Season2Saves.Accessor(season2);
        var choice = TestSeasons.ChoicesFor("s1").First(choice => accessor.GetChoiceValue(choice.ChoiceKey) != null);
        var current = accessor.GetChoiceValue(choice.ChoiceKey)!;
        var other = choice.Options.First(option => !option.Value.Equals(current, StringComparison.OrdinalIgnoreCase));
        var service = Service(TestSeasons.Registry);
        service.Saves.Add(season2);
        service.CascadeChoices = true;

        service.CascadeChoice(choice.ChoiceKey, current, "s1");
        Assert.False(service.IsModified(season2));
        Assert.Equal(0, service.Revision(season2));

        service.CascadeChoice(choice.ChoiceKey, other.Value, "s1");
        Assert.True(service.IsModified(season2));
        Assert.Equal(1, service.Revision(season2));
    }
}
