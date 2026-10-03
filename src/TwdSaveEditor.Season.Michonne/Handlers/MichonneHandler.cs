using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Michonne.Story;

namespace TwdSaveEditor.Season.Michonne.Handlers;

public class MichonneHandler(IDialogLogCompanions companions, IStorySaveFactory saves)
    : StorySeasonHandler(companions, saves), IInventoryHandler, IChoicePresetProvider
{
    private const string EndItKey = "Episode 101 - Did you try to end it";
    private const string ZacharyKey = "Episode 101 - Did you let Sam shoot Zachary";
    private const string RandallKey = "Episode 102 - What did you do to Randall";

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
        "Restarting an episode removes its saves and cuts the decision log back to its start. Earlier episodes are marked finished, so the game won't offer to randomise them.",
        "A chapter opens the way the developers' chapter menu does. Decisions from before it are kept; later ones are cleared to be made again.",
        "A slot left without a save would show as empty, so it gets a checkpoint at the episode's start.",
    ];

    public IReadOnlyList<ChoicePreset> Presets { get; } =
    [
        new("Merciful Michonne", [new(EndItKey, "lowered_the_gun"), new(ZacharyKey, "tried_to_save_zachary"), new(RandallKey, "showed_him_mercy")]),
        new("Ruthless Michonne", [new(EndItKey, "pulled_the_trigger"), new(ZacharyKey, "let_sam_take_her_revenge"), new(RandallKey, "bashed_randall_s_head_in")]),
    ];

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "Items live in the save the game resumes from. A new resume point starts with only what its scene hands out.",
        "\"Add items picked up earlier\" adds what Michonne found before the resume point and still has. It works until the game saves over the chapter.",
        "Only Episode 1 has items, and the boat's are taken on the way to Monroe. The screwdriver is one of two weapons found in the final escape.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => MichonneStory.Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => MichonneStory.Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => MichonneStory.Inventory.CarriedItems(slot);
}
