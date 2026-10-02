using System.Globalization;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S2.Accessors;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Handlers;

public class S2Handler : PropChoicesSeasonHandler, IChoiceImporter, ICompanionFileHandler, IResumePointHandler
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

    private static string Now() => DateTime.Now.ToString(SaveDateFormat, CultureInfo.InvariantCulture);

    public bool IsCompanionFile(string fileName) => !S2SlotFiles.IsSlotBundle(fileName);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        if (!S2SlotFiles.IsSlotBundle(bundleFileName))
            return [];

        var storage = S2SlotFiles.StorageName(bundleFileName);
        return directoryFileNames
            .Where(name => name.Equals(storage, StringComparison.OrdinalIgnoreCase)
                || S2SlotFiles.IsPage(bundleFileName, name)
                || S2SlotFiles.IsSave(bundleFileName, name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files)
    {
        slot.Checkpoints.Clear();
        slot.EventLog = null;

        var storage = files.FirstOrDefault(file => file.Name.EndsWith(S2SlotFiles.StorageSuffix, StringComparison.OrdinalIgnoreCase));
        if (storage != null && TryRead(() => EventLogCodec.ReadStorage(storage.Data)) is { } parsed)
        {
            slot.EventLog = new EventLog(storage.Name, parsed);
            var listed = parsed.Pages.Select(entry => entry.PageSymbol).ToHashSet();
            foreach (var file in files.Where(file => file.Name.EndsWith(S2SlotFiles.PageExtension, StringComparison.OrdinalIgnoreCase)
                && listed.Contains(TelltaleHash.ComputeCrc64(file.Name))))
            {
                if (TryRead(() => EventLogCodec.ReadPage(file.Data)) is { } page)
                    slot.EventLog.PageFiles.Add(new EventLogPageFile(file.Name, page));
            }
        }

        foreach (var file in files.Where(file => file.Name.EndsWith(S2SlotFiles.BundleExtension, StringComparison.OrdinalIgnoreCase)))
        {
            if (TryRead(() => BundleReader.Read(file.Data, file.Name)) is { } save)
            {
                save.DetectedSeasonKey = SeasonKey;
                slot.Checkpoints.Add(save);
            }
        }
    }

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot)
    {
        var files = new List<CompanionFile>();
        if (slot.EventLog is { } log)
        {
            if (log.StorageModified)
                files.Add(new CompanionFile(log.StorageName, EventLogCodec.WriteStorage(log.Storage)));

            files.AddRange(log.PageFiles.Where(page => page.Modified).Select(page => new CompanionFile(page.Name, EventLogCodec.WritePage(page.Page))));
        }

        files.AddRange(slot.Checkpoints.Where(save => save.Modified).Select(save => new CompanionFile(save.FileName, BundleWriter.Write(save))));
        return files;
    }

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot)
    {
        if (!S2SlotFiles.IsSlotBundle(slot.FileName))
            return [];

        var names = slot.Checkpoints.Select(save => save.FileName).ToList();
        if (slot.EventLog is { } log)
            names.AddRange(log.PageFiles.Select(page => page.Name).Prepend(log.StorageName));

        return names;
    }

    private static T? TryRead<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

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
