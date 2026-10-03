using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Story;

namespace TwdSaveEditor.Season.S3.Handlers;

public class S3Handler(IDialogLogCompanions companions, IStorySaveFactory saves)
    : StorySeasonHandler(companions, saves), IChoiceImporter, IInventoryHandler, IChoicePresetProvider
{
    private const string EndingKey = "Episode 205 - Ending Choice";
    private const string KilledKennyKey = "Episode 205 - Killed Kenny";
    private const string SidedKey = "Episode 303 - Who did you side with in the end";
    private const string FeelingsKey = "Episode 304 - Did you tell Kate that you have feelings for her";
    private const string JoanKey = "Episode 304 - Did you shoot Joan or take Clint’s deal";

    public override string SeasonKey => "s3";
    public override string Name => "A New Frontier (Season 3)";
    public override string ShortName => "S3";
    public override string FilePrefix => "wd3_";

    public override StorySeason Season => S3Story.Season;

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "Ties That Bind - Part One", 6),
        new(2, "Ties That Bind - Part Two", 6),
        new(3, "Above the Law", 6),
        new(4, "Thicker Than Water", 6),
        new(5, "From the Gallows", 6),
    ];

    public override IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Restarting an episode removes its save and cuts the decision log back to its start. Earlier episodes are marked finished, so the game won't offer to randomise them.",
        "A chapter opens the way the developers' chapter menu does. Decisions from before it are kept; later ones are cleared to be made again.",
        "A chapter tied to one Season 2 ending, such as a flashback, sets that ending.",
    ];

    public IReadOnlyList<ChoicePreset> Presets { get; } =
    [
        new("Season 2: Stayed with Kenny", [new(EndingKey, "kenny"), new(KilledKennyKey, "false")], RevealsEnding: true),
        new("Season 2: Wellington", [new(EndingKey, "wellington"), new(KilledKennyKey, "false")], RevealsEnding: true),
        new("Season 2: Went with Jane", [new(EndingKey, "jane"), new(KilledKennyKey, "true")], RevealsEnding: true),
        new("Season 2: Alone", [new(EndingKey, "alone"), new(KilledKennyKey, "true")], RevealsEnding: true),
        new("Leave with Kate", [new(SidedKey, "chose_to_leave_with_kate"), new(FeelingsKey, "told_kate_you_shared_her")]),
        new("Stand with David", [new(SidedKey, "stuck_to_david_s_plan"), new(FeelingsKey, "didn_t_share_kate_s")]),
        new("Shoot Joan", [new(JoanKey, "chose_to_shoot_joan")]),
        new("Take Clint's Deal", [new(JoanKey, "chose_to_take_the_deal")]),
    ];

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "Items live in the save the game resumes from. A new resume point starts with only what its scene hands out.",
        "\"Add items picked up earlier\" adds what Javier found before the resume point and still has; optional gifts count as given. It works until the game saves over the chapter.",
        "Only Episodes 1 and 2 have items; the list shows this episode's.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => S3Story.Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => S3Story.Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => S3Story.Inventory.CarriedItems(slot);
}
