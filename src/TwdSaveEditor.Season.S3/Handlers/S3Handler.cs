using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Inventory;
using TwdSaveEditor.Season.S3.Story;

namespace TwdSaveEditor.Season.S3.Handlers;

public class S3Handler : StorySeasonHandler, IChoiceImporter, IInventoryHandler
{
    private const string PreviousSeasonKey = "s2";

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
        "Season 3 works out its decisions from a log of what was played. Restarting an episode removes its save and cuts the log back to where that episode began; earlier episodes are marked as finished so the game does not offer to randomise their decisions.",
        "From a later chapter, a small save is written that opens the scene the way the developers' chapter menu does. Decisions made in scenes before the chapter are kept; the others are cleared so they can be made again.",
        "A chapter that belongs to one Season 2 ending, such as a flashback, sets that ending in the save.",
    ];

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "The items are kept in the save the game resumes from. Setting a new resume point writes a new save, which starts with nothing but what that scene hands out.",
        "\"Add items picked up earlier\" gives Javier what is found in the scenes before the resume point and not used up or given away by then. Where giving an item away is a choice, it is taken as given. It is offered for a chapter set here, until the game replaces the save with its own.",
        "Only Episodes 1 and 2 have items. The list holds the items of the episode in progress.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => S3Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => S3Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => S3Inventory.CarriedItems(slot);

    public bool CanImportFrom(SaveSlot source) => source.DetectedSeasonKey == PreviousSeasonKey && source.EventLog != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.EventLog == null)
            return;

        new StoryEventLog(target, Season).ReplacePreviousGameData(source.EventLog.Events.Select(entry => entry.DialogNode).OfType<ulong>());
        StoryChoiceAccessor.UpdateSavedLogic(target, Season);
    }
}
