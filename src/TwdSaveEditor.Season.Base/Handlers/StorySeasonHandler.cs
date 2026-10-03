using System.Globalization;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Handlers;

public abstract class StorySeasonHandler(IDialogLogCompanions companions, IStorySaveFactory saves) : SeasonHandlerBase, ICompanionFileHandler, IResumePointHandler
{
    private StoryResumePoint? _resume;

    public abstract StorySeason Season { get; }

    public StoryResumePoint Resume => _resume ??= new StoryResumePoint(Season);

    public IReadOnlyList<EpisodeInfo> ResumeEpisodes => Episodes;

    public abstract IReadOnlyList<string> ResumeNotes { get; }

    public override string GetEpisodeId(int episode) => Season.ProjectName(episode);

    public override SaveSlot CreateBlankSave(string fileName, string episodeId) =>
        saves.Create(fileName, Season.EpisodeNumber(episodeId), Season);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot) =>
        slot.Metadata != null && DialogLogFiles.IsSlotBundle(slot.FileName) ? new StoryChoiceAccessor(slot, Season) : null;

    public override void PopulateChoices(SaveSlot slot, int episode) =>
        Resume.RestartFromEpisode(slot, Math.Max(episode, StorySeason.FirstEpisode), Now());

    public ResumeState GetResumeState(SaveSlot slot) => Resume.GetState(slot);

    public void RestartFromEpisode(SaveSlot slot, int episode) => Resume.RestartFromEpisode(slot, episode, Now());

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) => Resume.GetChapters(episode);

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId) =>
        Resume.RestartFromChapter(slot, episode, chapterId, Now());

    public bool IsCompanionFile(string fileName) => !DialogLogFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames) =>
        companions.Find(bundleFileName, directoryFileNames);

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files) => companions.Attach(slot, files, SeasonKey);

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot) => companions.Build(slot);

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot) => companions.Names(slot);

    public bool CanImportFrom(SaveSlot source) =>
        Season.PreviousSeasonKey != null && source.DetectedSeasonKey == Season.PreviousSeasonKey && source.EventLog != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.EventLog == null)
        {
            return;
        }

        new StoryEventLog(target, Season).ReplacePreviousGameData(source.EventLog.Events.Select(entry => entry.DialogNode).OfType<ulong>());
        StoryChoiceAccessor.UpdateSavedLogic(target, Season);
    }

    private string Now() => DateTime.Now.ToString(Season.DateFormat, CultureInfo.InvariantCulture);
}
