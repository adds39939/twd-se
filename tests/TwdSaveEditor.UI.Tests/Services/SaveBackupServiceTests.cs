using FakeItEasy;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Tests.Services;

public class SaveBackupServiceTests
{
    private readonly IFileSystemService _fs = A.Fake<IFileSystemService>();
    private readonly IBackupFileResolver _files = A.Fake<IBackupFileResolver>();

    private static readonly SaveSlot Slot = new()
    {
        FilePath = "wd1_saveslot1.bundle",
        FileName = "wd1_saveslot1.bundle",
        OuterHeader = new MetaStreamHeader(),
        Files = [],
    };

    private SaveBackupService Service() => new(_fs, _files);

    [Fact]
    public async Task BackupBeforeSave_CopiesTheResolvedFilesIntoATimestampedFolder()
    {
        A.CallTo(() => _files.GetFilesToBackup(Slot)).Returns(["wd1_saveslot1.bundle", "_wd1_saveslot1_autosave.bundle"]);
        A.CallTo(() => _fs.BackupFiles(A<string>._, A<string[]>._)).Returns(true);

        var folder = await Service().BackupBeforeSave(Slot);

        Assert.NotNull(folder);
        Assert.Matches(@"^backup_\d{8}_\d{6}$", folder);
        A.CallTo(() => _fs.BackupFiles(folder, A<string[]>.That.IsSameSequenceAs(new[] { "wd1_saveslot1.bundle", "_wd1_saveslot1_autosave.bundle" })))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BackupBeforeSave_ReturnsNullWhenTheCopyFails()
    {
        A.CallTo(() => _files.GetFilesToBackup(Slot)).Returns(["wd1_saveslot1.bundle"]);
        A.CallTo(() => _fs.BackupFiles(A<string>._, A<string[]>._)).Returns(false);

        Assert.Null(await Service().BackupBeforeSave(Slot));
    }
}
