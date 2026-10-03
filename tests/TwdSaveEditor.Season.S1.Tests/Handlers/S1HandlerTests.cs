using System.Globalization;
using FakeItEasy;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Tests.Handlers;

public class S1HandlerTests
{
    private readonly IS1ResumePoint _resume = A.Fake<IS1ResumePoint>();
    private readonly IS1Inventory _inventory = A.Fake<IS1Inventory>();
    private readonly IS1SaveFactory _saves = A.Fake<IS1SaveFactory>();
    private readonly IS1CheckpointRefresher _checkpoints = A.Fake<IS1CheckpointRefresher>();
    private readonly ISaveBundleSerializer _serializer = A.Fake<ISaveBundleSerializer>();

    private S1Handler Handler() => new(_resume, _inventory, _saves, _checkpoints, _serializer);

    private static SaveSlot Slot() => SaveSlotFactory.Create("wd1_saveslot1.bundle", (BundleFileNames.SlotMetadata, new PropertySet()));

    [Fact]
    public void RestartFromChapter_PassesTheCurrentDateInTheSaveFormat()
    {
        var slot = Slot();
        var before = DateTime.Now.AddSeconds(-1);

        Handler().RestartFromChapter(slot, 5, "OnJewelryStore");

        A.CallTo(() => _resume.RestartFromChapter(slot, 5, "OnJewelryStore", A<string>.That.Matches(date =>
                DateTime.ParseExact(date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) >= before)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void CreateBlankSave_UsesTheEpisodeOfTheId()
    {
        var created = Slot();
        A.CallTo(() => _saves.Create("wd1_saveslot1.bundle", 3)).Returns(created);

        Assert.Same(created, Handler().CreateBlankSave("wd1_saveslot1.bundle", "WalkingDead103"));
    }

    [Fact]
    public void ChoiceAccessor_RefreshesTheGeneratedCheckpointAfterAChange()
    {
        var slot = Slot();

        var accessor = Handler().CreateChoiceAccessor(slot)!;
        accessor.SetChoiceValue("DougCarley Saved", "doug");

        A.CallTo(() => _checkpoints.Refresh(slot)).MustHaveHappenedOnceExactly();
        Assert.Equal("doug", accessor.GetChoiceValue("DougCarley Saved"));
    }

    [Fact]
    public void AttachCompanionFiles_MarksTheAutosaveDamagedWhenItCannotBeRead()
    {
        var slot = Slot();
        A.CallTo(() => _serializer.Read(A<byte[]>._, "_wd1_saveslot1_autosave.bundle")).Throws<InvalidDataException>();

        Handler().AttachCompanionFiles(slot, [new CompanionFile("_wd1_saveslot1_autosave.bundle", [1, 2, 3])]);

        Assert.Null(slot.Autosave);
        Assert.True(slot.AutosaveDamaged);
    }

    [Fact]
    public void AttachCompanionFiles_TagsTheAutosaveWithTheSeason()
    {
        var slot = Slot();
        var autosave = SaveSlotFactory.Create("_wd1_saveslot1_autosave.bundle", (BundleFileNames.SaveMetadata, new PropertySet()));
        A.CallTo(() => _serializer.Read(A<byte[]>._, autosave.FileName)).Returns(autosave);

        Handler().AttachCompanionFiles(slot, [new CompanionFile(autosave.FileName, [])]);

        Assert.Same(autosave, slot.Autosave);
        Assert.Equal("s1", autosave.DetectedSeasonKey);
    }

    [Fact]
    public void BuildCompanionFiles_WritesTheAutosaveThroughTheSerializer()
    {
        var slot = Slot();
        slot.Autosave = SaveSlotFactory.Create("_wd1_saveslot1_autosave.bundle", (BundleFileNames.SaveMetadata, new PropertySet()));
        A.CallTo(() => _serializer.Write(slot.Autosave)).Returns([9, 9]);

        var files = Handler().BuildCompanionFiles(slot);

        var file = Assert.Single(files);
        Assert.Equal("_wd1_saveslot1_autosave.bundle", file.Name);
        Assert.Equal([9, 9], file.Data);
    }

    [Fact]
    public void Inventory_IsDelegatedToTheInventoryService()
    {
        var slot = Slot();
        var held = new[] { new HeldItem("axe") };

        Handler().SetInventory(slot, held);

        A.CallTo(() => _inventory.SetItems(slot, held, null)).MustHaveHappenedOnceExactly();
    }
}
