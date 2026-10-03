using FakeItEasy;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Tests.Saves;

public class S1CheckpointRefresherTests
{
    private readonly IS1ResumePoint _resume = A.Fake<IS1ResumePoint>();
    private readonly IS1CheckpointBuilder _builder = A.Fake<IS1CheckpointBuilder>();
    private readonly IS1Inventory _inventory = A.Fake<IS1Inventory>();

    private S1CheckpointRefresher Refresher() => new(_resume, _builder, _inventory);

    private static SaveSlot SlotWithGeneratedAutosave(int episode, string chapterId, int serial)
    {
        var saved = new PropertySet();
        saved.SetString(SaveMetadataKeys.Episode, S1SlotFiles.EpisodeId(episode));
        saved.SetString(SaveMetadataKeys.ChapterId, chapterId);
        saved.SetInt(SaveMetadataKeys.Serial, serial);
        saved.SetString(SaveMetadataKeys.Date, "2026-01-02 03:04:05");

        var slot = SaveSlotFactory.Create("wd1_saveslot1.bundle", (BundleFileNames.SlotMetadata, new PropertySet()));
        slot.Autosave = SaveSlotFactory.Create("_wd1_saveslot1_autosave.bundle", (BundleFileNames.SaveMetadata, saved));
        return slot;
    }

    [Fact]
    public void Refresh_DoesNothingWithoutAGeneratedCheckpoint()
    {
        var slot = SlotWithGeneratedAutosave(5, "OnJewelryStore", 1);
        A.CallTo(() => _resume.GeneratedChapter(slot)).Returns(null);

        Refresher().Refresh(slot);

        A.CallTo(_builder).MustNotHaveHappened();
        A.CallTo(_inventory).MustNotHaveHappened();
    }

    [Fact]
    public void Refresh_RebuildsTheCheckpointAndKeepsTheItems()
    {
        var slot = SlotWithGeneratedAutosave(5, "OnJewelryStore", 7);
        var chapters = S1ChapterCatalog.ForEpisode(5)!;
        var chapter = chapters.Find("OnJewelryStore")!;
        var rebuilt = SaveSlotFactory.Create("_wd1_saveslot1_autosave.bundle", (BundleFileNames.SaveMetadata, new PropertySet()));
        var held = new[] { new HeldItem("axe") };
        A.CallTo(() => _resume.GeneratedChapter(slot)).Returns(chapter);
        A.CallTo(() => _inventory.GetState(slot)).Returns(new InventoryState("Lee", 5, null, [], held));
        A.CallTo(() => _builder.Build(slot, chapters, chapter, 7, "2026-01-02 03:04:05")).Returns(rebuilt);

        Refresher().Refresh(slot);

        Assert.Same(rebuilt, slot.Autosave);
        A.CallTo(() => _inventory.SetItems(slot, held, A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(chapters.DecisionFlags.Select(flag => flag.Key))))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Refresh_LeavesTheItemsWhenTheInventoryCannotBeEdited()
    {
        var slot = SlotWithGeneratedAutosave(5, "OnJewelryStore", 1);
        var chapters = S1ChapterCatalog.ForEpisode(5)!;
        var chapter = chapters.Find("OnJewelryStore")!;
        A.CallTo(() => _resume.GeneratedChapter(slot)).Returns(chapter);
        A.CallTo(() => _inventory.GetState(slot)).Returns(InventoryState.NotEditable("Lee", 5, "damaged"));

        Refresher().Refresh(slot);

        A.CallTo(() => _builder.Build(slot, chapters, chapter, 1, A<string>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _inventory.SetItems(A<SaveSlot>._, A<IReadOnlyList<HeldItem>>._, A<IReadOnlyCollection<string>?>._)).MustNotHaveHappened();
    }
}
