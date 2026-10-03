using FakeItEasy;
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

    public SaveEditorServiceTests()
    {
        A.CallTo(() => _registry.DetectFromFileName(A<string>._)).Returns(null);
        A.CallTo(() => _backupFiles.GetFilesToBackup(A<SaveSlot>._)).ReturnsLazily((SaveSlot slot) => [slot.FileName]);
        A.CallTo(() => _fs.BackupFiles(A<string>._, A<string[]>._))
            .ReturnsLazily((string folder, string[] _) => new BackupResult(folder, null));
        A.CallTo(() => _fs.WriteFile(A<string>._, A<byte[]>._)).Returns(true);
        A.CallTo(() => _fs.ListFiles(A<string>._)).Returns(Array.Empty<string>());
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
    public async Task SaveFile_ClearsOnlyTheSaveThatWasWritten()
    {
        var first = CreateSlot("wd2_saveslot1.bundle");
        var second = CreateSlot("wd3_saveslot1.bundle");
        var service = ServiceWith(first, second);
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
        var service = ServiceWith(slot);
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

        service.CascadeChoice(choice.ChoiceKey, other.Value, "s1");
        Assert.True(service.IsModified(season2));
    }
}
