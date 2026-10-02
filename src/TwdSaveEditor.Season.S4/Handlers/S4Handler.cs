using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S4.Collectibles;
using TwdSaveEditor.Season.S4.Story;

namespace TwdSaveEditor.Season.S4.Handlers;

public class S4Handler : StorySeasonHandler, IChoiceImporter, IChoicePresetProvider, IInventoryHandler
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
        "Season 4 works out its decisions from a log of what was played. Restarting an episode removes its save and cuts the log back to where that episode began; earlier episodes are marked as finished so the game does not offer to randomise their decisions.",
        "From a later chapter, a small save is written that opens the scene the way the developers' chapter menu does. Decisions made in scenes before the chapter are kept; the others are cleared so they can be made again.",
        "Episode 1 begins with the story builder, which asks about the earlier seasons again and adds its answers to the save. To keep the earlier seasons as set here, resume Episode 1 from \"Road Tile\" instead of its beginning.",
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
        "Season 4 has no items to carry. What Clementine collects instead are the collectibles of the four episodes, kept in the slot file as found and placed in her room.",
        "A collectible marked as found but not placed can still be placed in the room; one marked as placed should also be marked as found. The list covers every episode.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => S4Collectibles.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => S4Collectibles.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => [];
}
