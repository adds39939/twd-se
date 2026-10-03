using System.Globalization;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Handlers;

public class S1Handler(IS1ResumePoint resume, IS1Inventory inventory, IS1SaveFactory saves, IS1CheckpointRefresher checkpoints, ISaveBundleSerializer serializer)
    : SeasonHandlerBase, ICompanionFileHandler, IResumePointHandler, IInventoryHandler, IPropertyNameProvider, IChoicePresetProvider
{
    private static readonly ChoicePreset[] StoryPresets =
    [
        new("Side with Kenny",
        [
            new("Sided With Kenny", "true"),
            new("Helped Kill Larry", "true"),
            new("Fought Kenny", "false"),
            new("With Kenny", "true"),
            new("Lost Temper", "false"),
        ]),
        new("Side with Lilly",
        [
            new("Sided With Kenny", "false"),
            new("Helped Kill Larry", "false"),
            new("Left Lilly", "false"),
        ]),
        new("Save Carley", [new("DougCarley Saved", "carley")]),
        new("Save Doug", [new("DougCarley Saved", "doug")]),
        new("Ending: Clementine Shoots Lee",
        [
            new("Cut Off Arm", "true"),
            new("Clementine Shot Lee", "true"),
        ], RevealsEnding: true),
        new("Ending: Lee Is Left Behind",
        [
            new("Cut Off Arm", "false"),
            new("Clementine Shot Lee", "false"),
        ], RevealsEnding: true),
    ];

    private static readonly string[] SeasonsInSave = [S1ChoiceCatalog.MainSeasonKey, S1ChoiceCatalog.ExtraEpisodeSeasonKey];

    private static readonly EpisodeInfo[] MainEpisodes =
    [
        new(1, "A New Day", 7),
        new(2, "Starved for Help", 7),
        new(3, "Long Road Ahead", 7),
        new(4, "Around Every Corner", 7),
        new(5, "No Time Left", 7),
    ];

    private const string SaveDateFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly EpisodeInfo ExtraEpisode = new(S1ResumePoint.ExtraEpisode, "400 Days", 6);

    public override string SeasonKey => S1ChoiceCatalog.MainSeasonKey;
    public override string Name => "Season 1";
    public override string ShortName => "S1";
    public override string FilePrefix => "wd1_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } = MainEpisodes;

    public override IReadOnlyList<string> IncludedSeasonKeys => SeasonsInSave;

    public virtual IReadOnlyList<ChoicePreset> Presets => StoryPresets;

    public override (string SeasonKey, int Episode) DecisionGroupOf(int episode) =>
        episode == S1ResumePoint.ExtraEpisode ? (S1ChoiceCatalog.ExtraEpisodeSeasonKey, 1) : (S1ChoiceCatalog.MainSeasonKey, episode);

    public IReadOnlyList<EpisodeInfo> ResumeEpisodes { get; } = [.. MainEpisodes, ExtraEpisode];

    public IEnumerable<string> PropertyNames =>
    [
        .. S1ChoiceCatalog.All.Select(choice => PersistentKeys.SlotKey(S1ChoiceCatalog.PersistentEpisode(choice), choice.ChoiceKey)),
        .. ResumeEpisodes.Select(episode => SlotMetadataKeys.CompletedEpisode(episode.Number)),
        .. Enumerable.Range(PersistentKeys.FirstEpisode, PersistentKeys.LastEpisode - PersistentKeys.FirstEpisode + 1)
            .Select(PersistentKeys.TrackerContainer),
    ];

    public override string GetEpisodeId(int episode) => S1SlotFiles.EpisodeId(episode);

    public override SaveSlot CreateBlankSave(string fileName, string episodeId) =>
        saves.Create(fileName, S1SlotFiles.EpisodeNumber(episodeId) ?? 1);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot) =>
        slot.Metadata != null && S1SlotFiles.IsSlotBundle(slot.FileName) ? new S1ChoiceAccessor(slot, checkpoints) : null;

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
    }

    public IReadOnlyList<string> ResumeNotes { get; } =
    [
        "From the start of an episode, the slot is listed in the game as a new game until the first checkpoint: select it, pick the episode and press Play.",
        "From a later chapter, a small checkpoint is written. The game shows the scene before the chapter for a moment, then starts the chapter with the decisions set here and saves its own checkpoint.",
        "In 400 Days, the stories listed before the chosen chapter count as finished, with their decisions.",
    ];

    public ResumeState GetResumeState(SaveSlot slot) => resume.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => resume.RestartFromEpisode(slot, episode);

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => resume.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        resume.RestartFromChapter(slot, episode, chapterId, DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture));

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "The items are kept in the save the game resumes from. Setting a new resume point writes a new save, which starts with only what the developers' chapter setup hands out.",
        "\"Add items picked up earlier\" gives only what is certain in the order of the developers' chapter list: items whose scenes all lie before the chapter and that no scene since could have used up. It is offered for a chapter set here, until the game replaces the save with its own.",
        "Season 1 items open steps of its puzzles and its scenes are revisited, so look over the list before saving. The weapon follows the Episode 3 weapon decision.",
    ];

    public InventoryState GetInventory(SaveSlot slot) => inventory.GetState(slot);

    public void SetInventory(SaveSlot slot, IReadOnlyList<HeldItem> items) => inventory.SetItems(slot, items);

    public IReadOnlyList<HeldItem> GetCarriedItems(SaveSlot slot) => inventory.CarriedItems(slot);

    public bool IsCompanionFile(string fileName) => !S1SlotFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        if (!S1SlotFiles.IsSlotBundle(bundleFileName))
        {
            return [];
        }

        var autosave = S1SlotFiles.AutosaveName(bundleFileName);
        return directoryFileNames.Where(name => name.Equals(autosave, StringComparison.OrdinalIgnoreCase)).Take(1).ToList();
    }

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files)
    {
        var autosave = files.FirstOrDefault();
        if (autosave == null)
        {
            return;
        }

        try
        {
            slot.Autosave = serializer.Read(autosave.Data, autosave.Name);
            slot.Autosave.DetectedSeasonKey = SeasonKey;
            slot.AutosaveDamaged = slot.Autosave.Metadata == null || !slot.Autosave.Files.All(IsMetaStream);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            slot.Autosave = null;
            slot.AutosaveDamaged = true;
        }
    }

    private static bool IsMetaStream(BundleFileEntry file) =>
        file.Data.Length >= sizeof(uint)
        && BitConverter.ToUInt32(file.Data, 0) is MetaStreamHeader.MagicMsv5 or MetaStreamHeader.MagicMsv6;

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot) =>
        slot.Autosave == null ? [] : [new CompanionFile(slot.Autosave.FileName, serializer.Write(slot.Autosave))];

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot) =>
        S1SlotFiles.IsSlotBundle(slot.FileName) ? [S1SlotFiles.AutosaveName(slot.FileName)] : [];
}
