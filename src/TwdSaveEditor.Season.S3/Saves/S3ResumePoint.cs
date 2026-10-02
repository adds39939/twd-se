using System.Text.Json;
using System.Text.RegularExpressions;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Chapters;
using TwdSaveEditor.Season.S3.Decisions;

namespace TwdSaveEditor.Season.S3.Saves;

public static partial class S3ResumePoint
{
    public static ResumeState GetState(SaveSlot slot)
    {
        var metadata = slot.Metadata;
        var finished = LastFinished(slot);
        if (ResumeSave(slot) is { } save)
        {
            var saved = save.Metadata!;
            var episode = saved.GetInt(SaveMetadataKeys.Episode) ?? S3SlotFiles.FirstEpisode;
            var title = GeneratedChapter(save, episode)?.Title ?? Words().Replace(saved.GetString(S3SlotFiles.SavedScript) ?? string.Empty, " $1");
            return new ResumeState(episode, title, saved.GetString(SaveMetadataKeys.Date));
        }

        if (finished >= S3SlotFiles.LastEpisode)
            return new ResumeState(S3SlotFiles.LastEpisode, null, null, SeasonFinished: true);

        var next = Math.Clamp(Math.Max(metadata?.GetInt(SlotMetadataKeys.EpisodeInProgress) ?? S3SlotFiles.FirstEpisode, finished + 1), S3SlotFiles.FirstEpisode, S3SlotFiles.LastEpisode);
        var latest = metadata?.GetString(SlotMetadataKeys.LatestSave);
        var damaged = !string.IsNullOrEmpty(latest) && LatestSave(slot)?.Metadata == null;
        return new ResumeState(next, null, null, damaged);
    }

    public static SaveSlot? ResumeSave(SaveSlot slot)
    {
        var save = LatestSave(slot);
        return save?.Metadata is { } saved && (saved.GetInt(SaveMetadataKeys.Episode) ?? S3SlotFiles.FirstEpisode) > LastFinished(slot) ? save : null;
    }

    public static IReadOnlyList<ChapterInfo> GetChapters(int episode) =>
        S3ChapterCatalog.ForEpisode(episode)?.Chapters.Select(chapter => new ChapterInfo(chapter.Id, chapter.Title, chapter.Group)).ToList() ?? [];

    public static void RestartFromEpisode(SaveSlot slot, int episode)
    {
        var metadata = slot.Metadata
            ?? throw new InvalidOperationException("Cannot set the resume point: the save has no slot metadata.");

        episode = Math.Clamp(episode, S3SlotFiles.FirstEpisode, S3SlotFiles.LastEpisode);
        var log = new S3EventLog(slot);
        log.EnsurePreviousGameData();

        var decisions = new S3DecisionLog(slot);
        var kept = S3DecisionCatalog.All.Where(decision => decision.Episode < episode)
            .ToDictionary(decision => decision, decision => Chosen(decisions, decision));

        foreach (var save in slot.Checkpoints.Where(save => (save.Metadata?.GetInt(SaveMetadataKeys.Episode) ?? S3SlotFiles.LastEpisode) >= episode).ToList())
        {
            slot.Checkpoints.Remove(save);
            if (!slot.ObsoleteFileNames.Contains(save.FileName))
                slot.ObsoleteFileNames.Add(save.FileName);
        }

        log.TruncateFromEpisode(episode);
        for (var earlier = S3SlotFiles.FirstEpisode; earlier < episode; earlier++)
            log.FinishEpisode(earlier);

        foreach (var (decision, option) in kept)
            Restore(decisions, decision, option);

        var latest = slot.Checkpoints.MaxBy(save => save.Metadata?.GetInt(SaveMetadataKeys.Serial) ?? 0);
        metadata.SetInt(SlotMetadataKeys.EpisodeInProgress, episode);
        metadata.SetString(SlotMetadataKeys.LatestSave, latest?.FileName ?? string.Empty);
        SetNumber(metadata, S3SlotFiles.LastEpisodeFinished, episode - 1);
        metadata.SetInt(S3SlotFiles.EpisodesCompleted, episode - 1);
        for (var number = S3SlotFiles.FirstEpisode; number <= S3SlotFiles.LastEpisode; number++)
        {
            if (number < episode)
                metadata.SetBool(SlotMetadataKeys.CompletedEpisode(number), true);
            else
                metadata.Remove(Symbol.FromString(SlotMetadataKeys.CompletedEpisode(number)));
        }

        if (metadata.Find(S3SlotFiles.EpisodesSkipped) != null)
            metadata.SetString(S3SlotFiles.EpisodesSkipped, string.Empty);
        if (metadata.Find(S3SlotFiles.GeneratedChoices) != null)
            SetNumber(metadata, S3SlotFiles.GeneratedChoices, 0);
    }

    public static void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date)
    {
        var chapter = S3ChapterCatalog.ForEpisode(episode)?.Find(chapterId)
            ?? throw new ArgumentException($"Episode {episode} has no chapter {chapterId}.", nameof(chapterId));

        var decisions = new S3DecisionLog(slot);
        var decided = S3DecisionCatalog.All
            .Where(decision => decision.Episode == episode && chapter.Decided.Contains(decision.ChoiceKey, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(decision => decision, decision => Chosen(decisions, decision));

        RestartFromEpisode(slot, episode);
        if (chapter.StartsEpisode)
            return;

        var log = new S3EventLog(slot);
        log.BeginEpisode(episode);
        foreach (var (decision, option) in decided)
            Restore(decisions, decision, option);

        foreach (var flag in chapter.Flags)
        {
            if (S3DecisionCatalog.Find(flag.Key) is { } decision && decision.Find(Text(flag.Value)) is { } option)
                decisions.SetValue(decision, option);
        }

        var metadata = slot.Metadata!;
        var serial = (metadata.GetInt(SlotMetadataKeys.LatestSerial) ?? 0) + 1;
        var save = S3CheckpointBuilder.Build(slot, episode, chapter, serial, date);
        slot.Checkpoints.RemoveAll(existing => existing.FileName.Equals(save.FileName, StringComparison.OrdinalIgnoreCase));
        slot.Checkpoints.Add(save);
        slot.ObsoleteFileNames.RemoveAll(name => name.Equals(save.FileName, StringComparison.OrdinalIgnoreCase));
        log.AppendSaveSerial(serial);

        metadata.SetString(SlotMetadataKeys.LatestSave, save.FileName);
        metadata.SetInt(SlotMetadataKeys.LatestSerial, serial);
    }

    public static int LastFinished(SaveSlot slot) => slot.Metadata?.Find(S3SlotFiles.LastEpisodeFinished)?.Value switch
    {
        FloatValue number => (int)number.Value,
        IntValue number => number.Value,
        _ => 0,
    };

    public static PropertySet? Properties(SaveSlot save, ulong name) =>
        save.FindFile(name) is { } file && BundleReader.TryParseProperties(file) ? file.Properties : null;

    private static SaveSlot? LatestSave(SaveSlot slot)
    {
        var latest = slot.Metadata?.GetString(SlotMetadataKeys.LatestSave);
        return string.IsNullOrEmpty(latest)
            ? null
            : slot.Checkpoints.FirstOrDefault(candidate => candidate.FileName.Equals(latest, StringComparison.OrdinalIgnoreCase));
    }

    private static S3DecisionOption Chosen(S3DecisionLog decisions, S3Decision decision) =>
        decisions.IsSet(decision)
            ? decisions.GetOption(decision)!
            : decision.Options.FirstOrDefault(option => option.Expression.Length == 0) ?? decision.Options[0];

    private static void Restore(S3DecisionLog decisions, S3Decision decision, S3DecisionOption option)
    {
        if (!ReferenceEquals(decisions.GetOption(decision), option) || (option.Expression.Length > 0 && !decisions.IsSet(decision)))
            decisions.SetValue(decision, option);
    }

    private static S3Chapter? GeneratedChapter(SaveSlot save, int episode)
    {
        if (save.Metadata?.GetString(S3SlotFiles.SavedScript) is not { } script
            || S3ChapterCatalog.ForEpisode(episode) is not { } chapters
            || Properties(save, S3SlotFiles.ScriptProperties)?.GetString(S3CheckpointBuilder.PreviousScript) != S3CheckpointBuilder.DeveloperMenuScript)
        {
            return null;
        }

        var flags = Properties(save, S3SlotFiles.LogicGameProperties);
        return chapters.Chapters
            .Where(chapter => !chapter.StartsEpisode && chapter.Script.Equals(script, StringComparison.OrdinalIgnoreCase))
            .Where(chapter => chapter.Flags.All(flag => flags?.Find(flag.Key) != null))
            .MaxBy(chapter => chapter.Flags.Count);
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText(),
    };

    private static void SetNumber(PropertySet properties, string key, int value)
    {
        switch (properties.Find(key)?.Value)
        {
            case FloatValue number:
                number.Value = value;
                break;
            case IntValue:
                properties.SetInt(key, value);
                break;
            default:
                properties.Set(Symbol.FromString(key), new Symbol(TelltaleTypes.Float), new FloatValue(value));
                break;
        }
    }

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex Words();
}
