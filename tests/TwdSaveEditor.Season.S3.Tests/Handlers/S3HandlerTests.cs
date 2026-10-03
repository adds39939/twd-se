using FakeItEasy;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S3.Story;

namespace TwdSaveEditor.Season.S3.Tests.Handlers;

public class S3HandlerTests
{
    private readonly IDialogLogCompanions _companions = A.Fake<IDialogLogCompanions>();
    private readonly IStorySaveFactory _saves = A.Fake<IStorySaveFactory>();

    private S3Handler Handler() => new(_companions, _saves);

    private static SaveSlot Slot() => SaveSlotFactory.Create("wd3_saveslot1.bundle", (BundleFileNames.SlotMetadata, new PropertySet()));

    [Fact]
    public void CreateBlankSave_AsksTheFactoryForTheEpisodeOfTheSeason()
    {
        var created = Slot();
        A.CallTo(() => _saves.Create("wd3_saveslot1.bundle", 2, S3Story.Season)).Returns(created);

        Assert.Same(created, Handler().CreateBlankSave("wd3_saveslot1.bundle", "WalkingDead302"));
    }

    [Fact]
    public void AttachCompanionFiles_TagsTheFilesWithTheSeasonKey()
    {
        var slot = Slot();
        IReadOnlyList<CompanionFile> files = [new CompanionFile("_wd3_saveslot1_id.estore", [])];

        Handler().AttachCompanionFiles(slot, files);

        A.CallTo(() => _companions.Attach(slot, files, "s3")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void CompanionFileQueries_AreDelegated()
    {
        var slot = Slot();
        var directory = new[] { "a", "b" };
        A.CallTo(() => _companions.Find(slot.FileName, directory)).Returns(["a"]);
        A.CallTo(() => _companions.Names(slot)).Returns(["n"]);

        var handler = Handler();

        Assert.Equal(["a"], handler.FindCompanionFiles(slot.FileName, directory));
        Assert.Equal(["n"], handler.GetCompanionFileNames(slot));
    }
}
