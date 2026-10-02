using System.Globalization;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Handlers;

public class S1Handler : SeasonHandlerBase, ICompanionFileHandler, IResumePointHandler, IPropertyNameProvider
{
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
        S1SaveFactory.Create(fileName, S1SlotFiles.EpisodeNumber(episodeId) ?? 1);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot) =>
        slot.Metadata != null && S1SlotFiles.IsSlotBundle(slot.FileName) ? new S1ChoiceAccessor(slot) : null;

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
    }

    public ResumeState GetResumeState(SaveSlot slot) => S1ResumePoint.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => S1ResumePoint.RestartFromEpisode(slot, episode);

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => S1ResumePoint.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        S1ResumePoint.RestartFromChapter(slot, episode, chapterId, DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture));

    public bool IsCompanionFile(string fileName) => !S1SlotFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        if (!S1SlotFiles.IsSlotBundle(bundleFileName))
            return [];

        var autosave = S1SlotFiles.AutosaveName(bundleFileName);
        return directoryFileNames.Where(name => name.Equals(autosave, StringComparison.OrdinalIgnoreCase)).Take(1).ToList();
    }

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files)
    {
        var autosave = files.FirstOrDefault();
        if (autosave == null)
            return;

        try
        {
            slot.Autosave = BundleReader.Read(autosave.Data, autosave.Name);
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
        slot.Autosave == null ? [] : [new CompanionFile(slot.Autosave.FileName, BundleWriter.Write(slot.Autosave))];

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot) =>
        S1SlotFiles.IsSlotBundle(slot.FileName) ? [S1SlotFiles.AutosaveName(slot.FileName)] : [];
}
