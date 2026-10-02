using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Michonne.Story;

namespace TwdSaveEditor.Season.Michonne.Handlers;

public class MichonneHandler : StorySeasonHandler, IInventoryHandler
{
    public override string SeasonKey => "michonne";
    public override string Name => "Michonne";
    public override string ShortName => "M";
    public override string FilePrefix => "wdm_";

    public override StorySeason Season => MichonneStory.Season;

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "In Too Deep", 6),
        new(2, "Give No Shelter", 6),
        new(3, "What We Deserve", 6),
    ];

    public override IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Michonne works out its decisions from a log of what was played. Restarting an episode removes its saves and cuts the log back to where that episode began; earlier episodes are marked as finished so the game does not offer to randomise their decisions.",
        "From a later chapter, a small checkpoint is written that opens the scene the way the developers' chapter menu does. Decisions made in scenes before the chapter are kept; the others are cleared so they can be made again.",
        "The game lists a slot without any save as empty, so a slot that would be left with none gets a checkpoint that starts the episode.",
    ];

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "The items are kept in the save the game resumes from. Setting a new resume point writes a new checkpoint, which starts with nothing but what that scene hands out.",
        "\"Add items picked up earlier\" gives Michonne what is found in the scenes before the resume point and not taken away by then. It is offered for a chapter set here, until the game replaces the checkpoint with its own.",
        "Only Episode 1 has items, and everything from the boat is taken away on the way to Monroe. The screwdriver is one of the two weapons that can be picked up during the escape, the last scene of the episode.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => MichonneStory.Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => MichonneStory.Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => MichonneStory.Inventory.CarriedItems(slot);
}
