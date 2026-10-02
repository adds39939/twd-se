using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S2.Chapters;
using TwdSaveEditor.Season.S2.Decisions;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2ResumePoint
{
    public const int FirstEpisode = 1;
    public const int LastEpisode = 5;

    public static ResumeState GetState(SaveSlot slot)
    {
        var metadata = slot.Metadata;
        var progress = metadata?.GetString(SlotMetadataKeys.EpisodeInProgress);
        if (S2SlotFiles.CompletedEpisodes(progress, LastEpisode) is { } completed)
        {
            return completed >= LastEpisode
                ? new ResumeState(LastEpisode, null, null, SeasonFinished: true)
                : new ResumeState(Math.Max(completed, 0) + 1, null, null);
        }

        var episode = Math.Clamp(S2SlotFiles.EpisodeNumber(progress) ?? FirstEpisode, FirstEpisode, LastEpisode);
        var latest = metadata?.GetString(SlotMetadataKeys.LatestSave);
        if (string.IsNullOrEmpty(latest))
            return new ResumeState(episode, null, null);

        var save = slot.Checkpoints.FirstOrDefault(candidate => candidate.FileName.Equals(latest, StringComparison.OrdinalIgnoreCase));
        if (save?.Metadata is not { } saved)
            return new ResumeState(episode, null, null, true);

        var savedEpisode = S2SlotFiles.EpisodeNumber(saved.GetString(SaveMetadataKeys.Episode)) ?? episode;
        if (savedEpisode < episode)
            return new ResumeState(episode, null, null);

        var chapter = GeneratedChapter(save, savedEpisode);
        return chapter is { StartsEpisode: true }
            ? new ResumeState(savedEpisode, null, null)
            : new ResumeState(savedEpisode, chapter?.Title ?? saved.GetString(SaveMetadataKeys.ChapterId) ?? string.Empty, saved.GetString(SaveMetadataKeys.Date));
    }

    public static IReadOnlyList<ChapterInfo> GetChapters(int episode) =>
        S2ChapterCatalog.ForEpisode(episode)?.Chapters.Select(chapter => new ChapterInfo(chapter.Id, chapter.Title, chapter.Group)).ToList() ?? [];

    public static void RestartFromEpisode(SaveSlot slot, int episode, string date)
    {
        episode = Math.Clamp(episode, FirstEpisode, LastEpisode);
        Rewind(slot, episode);
        if (slot.Checkpoints.Count == 0 && S2ChapterCatalog.ForEpisode(episode) is { } chapters)
            AddCheckpoint(slot, chapters, chapters.Opening, date);
    }

    public static void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date)
    {
        var chapters = S2ChapterCatalog.ForEpisode(episode)
            ?? throw new ArgumentException($"Episode {episode} has no chapter list.", nameof(episode));
        var chapter = chapters.Find(chapterId)
            ?? throw new ArgumentException($"Episode {episode} has no chapter {chapterId}.", nameof(chapterId));

        if (chapter.StartsEpisode)
        {
            RestartFromEpisode(slot, episode, date);
            return;
        }

        var log = new S2EventLogEditor(slot);
        var made = S2DecisionCatalog.All
            .Where(decision => decision.Episode == episode && chapter.Decided.Contains(decision.ChoiceKey, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(decision => decision, decision => log.FindSeen(decision) ?? decision.Options[0]);

        Rewind(slot, episode);
        AddCheckpoint(slot, chapters, chapter, date);
        foreach (var (decision, option) in made)
            log.SetValue(decision, option);
    }

    private static void Rewind(SaveSlot slot, int episode)
    {
        var metadata = slot.Metadata
            ?? throw new InvalidOperationException("Cannot set the resume point: the save has no slot metadata.");

        slot.EventLog ??= S2EventLogFactory.Create(slot.FileName);

        var log = new S2EventLogEditor(slot);
        var earlier = S2DecisionCatalog.All.Where(decision => decision.Episode < episode).ToList();
        var made = earlier.ToDictionary(decision => decision, decision => log.FindSeen(decision) ?? decision.Options[0]);

        foreach (var save in slot.Checkpoints.Where(save => (S2SlotFiles.EpisodeNumber(save.Metadata?.GetString(SaveMetadataKeys.Episode)) ?? LastEpisode) >= episode).ToList())
        {
            slot.Checkpoints.Remove(save);
            if (!slot.ObsoleteFileNames.Contains(save.FileName))
                slot.ObsoleteFileNames.Add(save.FileName);
        }

        var latest = slot.Checkpoints.MaxBy(save => save.Metadata?.GetInt(SaveMetadataKeys.Serial) ?? 0);
        var serial = latest?.Metadata?.GetInt(SaveMetadataKeys.Serial) ?? 0;
        metadata.SetString(SlotMetadataKeys.EpisodeInProgress, S2SlotFiles.EpisodeId(episode));
        metadata.SetString(SlotMetadataKeys.LatestSave, latest?.FileName ?? string.Empty);
        metadata.SetInt(SlotMetadataKeys.LatestSerial, serial);

        log.TruncateAfterSerial(serial);
        foreach (var decision in S2DecisionCatalog.All.Where(decision => decision.Episode >= episode))
            log.Clear(decision);

        foreach (var (decision, option) in made)
            log.SetValue(decision, option);
    }

    private static void AddCheckpoint(SaveSlot slot, S2EpisodeChapters episode, S2Chapter chapter, string date)
    {
        var metadata = slot.Metadata!;
        var serial = (metadata.GetInt(SlotMetadataKeys.LatestSerial) ?? 0) + 1;
        var fileName = NextCheckpointName(slot);

        slot.Checkpoints.Add(S2CheckpointBuilder.Build(slot, episode, chapter, fileName, serial, date));
        slot.ObsoleteFileNames.RemoveAll(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        new S2EventLogEditor(slot).AppendSaveSerial(serial);

        metadata.SetString(SlotMetadataKeys.LatestSave, fileName);
        metadata.SetInt(SlotMetadataKeys.LatestSerial, serial);
    }

    private static string NextCheckpointName(SaveSlot slot)
    {
        for (var index = 1; ; index++)
        {
            var name = S2SlotFiles.SaveName(slot.FileName, S2SlotFiles.CheckpointName + index);
            if (!slot.Checkpoints.Any(save => save.FileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }

    private static S2Chapter? GeneratedChapter(SaveSlot save, int episode)
    {
        if (save.Metadata?.GetString(S2CheckpointBuilder.SavedScript) is not { } script || S2ChapterCatalog.ForEpisode(episode) is not { } chapters)
            return null;

        if (script.Equals(chapters.Opening.Script, StringComparison.OrdinalIgnoreCase))
            return chapters.Opening;

        if (Properties(save, S2SlotFiles.ScriptProperties)?.GetString(S2CheckpointBuilder.PreviousScript) != S2CheckpointBuilder.DeveloperMenuScript)
            return null;

        var flags = Properties(save, S2SlotFiles.LogicGameProperties);
        return chapters.Chapters
            .Where(chapter => !chapter.StartsEpisode && chapter.Script.Equals(script, StringComparison.OrdinalIgnoreCase))
            .Where(chapter => chapter.Flags.All(flag => flags?.Find(flag.Key) != null))
            .MaxBy(chapter => chapter.Flags.Count);
    }

    private static PropertySet? Properties(SaveSlot save, ulong name) =>
        save.FindFile(name) is { } file && BundleReader.TryParseProperties(file) ? file.Properties : null;
}
