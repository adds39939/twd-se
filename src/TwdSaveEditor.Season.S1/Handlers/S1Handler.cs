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
        "From an episode's start, the game lists the slot as a new game: select it, pick the episode and press Play.",
        "From a chapter, the game briefly shows the scene before it, then starts the chapter with these decisions.",
        "In 400 Days, the stories before the chosen one count as finished.",
    ];

    public ResumeState GetResumeState(SaveSlot slot) => resume.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => resume.RestartFromEpisode(slot, episode);

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => resume.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        resume.RestartFromChapter(slot, episode, chapterId, DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture));

    public IReadOnlyList<string> InventoryNotes { get; } =
    [
        "Items live in the save the game resumes from. A new resume point starts with only what its chapter hands out.",
        "\"Add items picked up earlier\" adds only items certain to be held: found before the chapter and not used since. It works until the game saves over the chapter.",
        "Items unlock puzzle steps and scenes repeat, so check the list before saving. The weapon follows the Episode 3 weapon decision.",
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
        catch (InvalidDataException)
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
