using System.Globalization;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S2.Accessors;
using TwdSaveEditor.Season.S2.Inventory;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Handlers;

public class S2Handler : PropChoicesSeasonHandler, IChoiceImporter, ICompanionFileHandler, IResumePointHandler, IInventoryHandler
{
    private const string SaveDateFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] ImportedSeasons = [S1ChoiceCatalog.MainSeasonKey, S1ChoiceCatalog.ExtraEpisodeSeasonKey];

    public override string SeasonKey => "s2";
    public override string Name => "Season 2";
    public override string ShortName => "S2";
    public override string FilePrefix => "wd2_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "All That Remains", 7),
        new(2, "A House Divided", 7),
        new(3, "In Harm's Way", 7),
        new(4, "Amid the Ruins", 7),
        new(5, "No Going Back", 7),
    ];

    public override IReadOnlyList<string> ImportsFromSeasonKeys => ImportedSeasons;

    protected override string ChoicesFileName => BundleFileNames.Season1Choices;

    public override string GetEpisodeId(int episode) => $"WalkingDead20{episode}";

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.Choices != null && S2SlotFiles.IsSlotBundle(slot.FileName) ? new S2ChoiceAccessor(slot) : null;

    public override SaveSlot CreateBlankSave(string fileName, string episodeId) => S2SaveFactory.Create(fileName, episodeId);

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
        var accessor = new S2ChoiceAccessor(slot);
        foreach (var choice in S1ChoiceCatalog.All.Where(choice => accessor.GetChoiceValue(choice.ChoiceKey) == null))
            accessor.ApplyChoice(choice, 0);

        foreach (var choice in GetChoicesUpTo(episode - 1).Where(choice => choice.SeasonKey == SeasonKey))
            accessor.ApplyChoice(choice, 0);

        S2ResumePoint.RestartFromEpisode(slot, episode, Now());
    }

    public IReadOnlyList<EpisodeInfo> ResumeEpisodes => Episodes;

    public IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Season 2 works out its decisions from a log of what was played. Saves of the chosen episode and of later episodes are removed, and the log is cut back to match.",
        "From a later chapter, a small checkpoint is written that opens the scene the way the developers' chapter menu does. The game then saves its own checkpoints as you play.",
        "Decisions of the chosen episode are kept when their scene comes before the chapter. The others are cleared so they can be made again in the game.",
    ];

    public ResumeState GetResumeState(SaveSlot slot) => S2ResumePoint.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => S2ResumePoint.RestartFromEpisode(slot, episode, Now());

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => S2ResumePoint.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        S2ResumePoint.RestartFromChapter(slot, episode, chapterId, Now());

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "The items are kept in the save the game resumes from. Setting a new resume point writes a new save, which starts with only what that scene gives Clementine.",
        "\"Add items picked up earlier\" gives her what is found in the scenes before the resume point and not taken away again by then, and the items the episode starts with for the decisions of earlier episodes. Items found earlier in the same scene are left out.",
        "The list holds the items of the episode in progress; the game has no icons for items of other episodes.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => S2Inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => S2Inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => S2Inventory.CarriedItems(slot);

    private static string Now() => DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture);

    public bool IsCompanionFile(string fileName) => !S2SlotFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames) =>
        DialogLogCompanions.Find(bundleFileName, directoryFileNames);

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files) => DialogLogCompanions.Attach(slot, files, SeasonKey);

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot) => DialogLogCompanions.Build(slot);

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot) => DialogLogCompanions.Names(slot);

    public bool CanImportFrom(SaveSlot source)
        => source.DetectedSeasonKey == S1ChoiceCatalog.MainSeasonKey && source.Metadata != null && source.Choices != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.Choices == null || target.Choices == null) return;

        var sourceAccessor = new S1ChoiceAccessor(source);
        var targetAccessor = new S2ChoiceAccessor(target);

        foreach (var choice in S1ChoiceCatalog.All)
        {
            if (sourceAccessor.GetChoiceValue(choice.ChoiceKey) is { } value)
                targetAccessor.SetChoiceValue(choice.ChoiceKey, value);
        }

        for (var episode = PersistentKeys.FirstEpisode; episode <= PersistentKeys.LastEpisode; episode++)
        {
            if (source.Choices.Find(Symbol.FromString(PersistentKeys.TrackerContainer(episode)))?.Value is RawBytesValue tracker)
                target.Choices.Set(Symbol.FromString(S2SlotFiles.TrackerContainer(episode)), tracker.TypeSymbol, new RawBytesValue([.. tracker.Data], tracker.TypeSymbol));
        }
    }
}
