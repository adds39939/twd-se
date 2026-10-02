using System.Globalization;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Accessors;
using TwdSaveEditor.Season.S3.Inventory;
using TwdSaveEditor.Season.S3.Saves;

namespace TwdSaveEditor.Season.S3.Handlers;

public class S3Handler : SeasonHandlerBase, ICompanionFileHandler, IResumePointHandler, IChoiceImporter, IInventoryHandler
{
    private const string SaveDateFormat = "yyyy-MM-dd HH:mm";
    private const string PreviousSeasonKey = "s2";
    private const string ProjectPrefix = "WalkingDead30";

    public override string SeasonKey => "s3";
    public override string Name => "A New Frontier (Season 3)";
    public override string ShortName => "S3";
    public override string FilePrefix => "wd3_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "Ties That Bind - Part One", 6),
        new(2, "Ties That Bind - Part Two", 6),
        new(3, "Above the Law", 6),
        new(4, "Thicker Than Water", 6),
        new(5, "From the Gallows", 6),
    ];

    public IReadOnlyList<EpisodeInfo> ResumeEpisodes => Episodes;

    public IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Season 3 works out its decisions from a log of what was played. Restarting an episode removes its save and cuts the log back to where that episode began; earlier episodes are marked as finished so the game does not offer to randomise their decisions.",
        "From a later chapter, a small save is written that opens the scene the way the developers' chapter menu does. Decisions made in scenes before the chapter are kept; the others are cleared so they can be made again.",
        "A chapter that belongs to one Season 2 ending, such as a flashback, sets that ending in the save.",
    ];

    public override string GetEpisodeId(int episode) => S3SlotFiles.ProjectName(episode);

    public override SaveSlot CreateBlankSave(string fileName, string episodeId) =>
        S3SaveFactory.Create(fileName, EpisodeNumber(episodeId));

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot) =>
        slot.Metadata != null && DialogLogFiles.IsSlotBundle(slot.FileName) ? new S3ChoiceAccessor(slot) : null;

    public override void PopulateChoices(SaveSlot slot, int episode) => S3ResumePoint.RestartFromEpisode(slot, Math.Max(episode, S3SlotFiles.FirstEpisode));

    public ResumeState GetResumeState(SaveSlot slot) => S3ResumePoint.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => S3ResumePoint.RestartFromEpisode(slot, episode);

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => S3ResumePoint.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        S3ResumePoint.RestartFromChapter(slot, episode, chapterId, DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture));

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "The items are kept in the save the game resumes from. Setting a new resume point writes a new save, which starts with nothing but what that scene hands out.",
        "\"Add items picked up earlier\" gives Javier what is found in the scenes before the resume point and not used up or given away by then. Where giving an item away is a choice, it is taken as given. It is offered for a chapter set here, until the game replaces the save with its own.",
        "Only Episodes 1 and 2 have items. The list holds the items of the episode in progress.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => S3Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => S3Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => S3Inventory.CarriedItems(slot);

    public bool IsCompanionFile(string fileName) => !DialogLogFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames) =>
        DialogLogCompanions.Find(bundleFileName, directoryFileNames);

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files) => DialogLogCompanions.Attach(slot, files, SeasonKey);

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot) => DialogLogCompanions.Build(slot);

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot) => DialogLogCompanions.Names(slot);

    public bool CanImportFrom(SaveSlot source) => source.DetectedSeasonKey == PreviousSeasonKey && source.EventLog != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.EventLog == null)
            return;

        new S3EventLog(target).ReplacePreviousGameData(source.EventLog.Events.Select(entry => entry.DialogNode).OfType<ulong>());
        S3ChoiceAccessor.UpdateSavedLogic(target);
    }

    private static int EpisodeNumber(string episodeId) =>
        episodeId.StartsWith(ProjectPrefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(episodeId[ProjectPrefix.Length..], out var episode)
            ? episode
            : S3SlotFiles.FirstEpisode;
}
