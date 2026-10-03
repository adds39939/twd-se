using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S4.Collectibles;
using TwdSaveEditor.Season.S4.Story;

namespace TwdSaveEditor.Season.S4.Handlers;

public class S4Handler(IDialogLogCompanions companions, IStorySaveFactory saves, IS4Collectibles collectibles)
    : StorySeasonHandler(companions, saves), IChoiceImporter, IChoicePresetProvider, IInventoryHandler
{
    private const string FollowedKey = "Episode 402 - Follow Violet or Louis";
    private const string SavedKey = "Episode 402 - Save Violet or Louis";
    private const string TrustedKey = "Episode 404 - Trusted AJ to make his own decisions";

    public override string SeasonKey => "s4";
    public override string Name => "The Final Season (Season 4)";
    public override string ShortName => "S4";
    public override string FilePrefix => "wd4_";

    public override StorySeason Season => S4Story.Season;

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "Done Running", 6),
        new(2, "Suffer the Children", 6),
        new(3, "Broken Toys", 6),
        new(4, "Take Us Back", 6),
    ];

    public override IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Restarting an episode removes its save and cuts the decision log back to its start. Earlier episodes are marked finished, so the game won't offer to randomise them.",
        "A chapter opens the way the developers' chapter menu does. Decisions from before it are kept; later ones are cleared to be made again.",
        "Episode 1 opens with the story builder, which asks about the earlier seasons again. To keep the choices set here, start Episode 1 from \"Road Tile\".",
    ];

    public IReadOnlyList<ChoicePreset> Presets { get; } =
    [
        new("Save Louis Path",
        [
            new(FollowedKey, "helped_louis_tune_the_piano"),
            new(SavedKey, "rescued_louis_instead_of_violet"),
        ]),
        new("Save Violet Path",
        [
            new(FollowedKey, "spent_time_stargazing_with_violet"),
            new(SavedKey, "rescued_violet_instead_of_louis"),
        ]),
        new("Trust AJ Path",
        [
            new(TrustedKey, "trusted_aj_to_make_his"),
        ], RevealsEnding: true),
    ];

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "Season 4 has no inventory. Instead, Clementine's collectibles from all four episodes are tracked as found and as placed in her room.",
        "A found collectible can still be placed in the room; a placed one should also be marked found.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => collectibles.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => collectibles.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => [];
}
