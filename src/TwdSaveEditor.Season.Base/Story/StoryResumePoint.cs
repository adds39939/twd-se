using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed partial class StoryResumePoint(StorySeason season)
{
    private readonly StoryCheckpointBuilder _builder = new(season);

    public ResumeState GetState(SaveSlot slot)
    {
        var metadata = slot.Metadata;
        var finished = LastFinished(slot);
        if (ResumeSave(slot) is { } save)
        {
            var saved = save.Metadata!;
            var episode = saved.GetInt(SaveMetadataKeys.Episode) ?? StorySeason.FirstEpisode;
            var title = GeneratedChapter(save, episode)?.Title
                ?? Chapter(save, episode)?.Title
                ?? Words().Replace(saved.GetString(StoryFiles.SavedScript) ?? string.Empty, " $1");
            return new ResumeState(episode, title, saved.GetString(SaveMetadataKeys.Date));
        }

        if (finished >= season.LastEpisode)
        {
            return new ResumeState(season.LastEpisode, null, null, SeasonFinished: true);
        }

        var next = Math.Clamp(Math.Max(metadata?.GetInt(SlotMetadataKeys.EpisodeInProgress) ?? StorySeason.FirstEpisode, finished + 1), StorySeason.FirstEpisode, season.LastEpisode);
        var latest = metadata?.GetString(SlotMetadataKeys.LatestSave);
        var damaged = !string.IsNullOrEmpty(latest) && LatestSave(slot)?.Metadata == null;
        return new ResumeState(next, null, null, damaged);
    }

    public SaveSlot? ResumeSave(SaveSlot slot)
    {
        var save = LatestSave(slot);
        if (save?.Metadata is not { } saved)
        {
            return null;
        }

        var episode = saved.GetInt(SaveMetadataKeys.Episode) ?? StorySeason.FirstEpisode;
        return episode > LastFinished(slot) && GeneratedChapter(save, episode) is not { StartsEpisode: true } ? save : null;
    }

    public IReadOnlyList<ChapterInfo> GetChapters(int episode) =>
        season.ChaptersOf(episode)?.Chapters.Select(chapter => new ChapterInfo(chapter.Id, chapter.Title, chapter.Group)).ToList() ?? [];

    public void RestartFromEpisode(SaveSlot slot, int episode, string date)
    {
        episode = Math.Clamp(episode, StorySeason.FirstEpisode, season.LastEpisode);
        Rewind(slot, episode);
        if (season.ChapterSaves && slot.Checkpoints.Count == 0 && season.ChaptersOf(episode) is { } chapters)
        {
            AddSave(slot, episode, chapters.Opening, date);
        }
    }

    public void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date)
    {
        var chapter = season.ChaptersOf(episode)?.Find(chapterId)
            ?? throw new ArgumentException($"Episode {episode} has no chapter {chapterId}.", nameof(chapterId));

        if (chapter.StartsEpisode)
        {
            RestartFromEpisode(slot, episode, date);
            return;
        }

        var decisions = new StoryDecisionLog(slot, season);
        var decided = season.Decisions
            .Where(decision => decision.Episode == episode && chapter.Decided.Contains(decision.ChoiceKey, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var made = Made(decisions, decided);

        Rewind(slot, episode);

        new StoryEventLog(slot, season).BeginEpisode(episode);
        Restore(decisions, decided, made);

        foreach (var flag in chapter.Flags)
        {
            if (season.FindDecision(flag.Key) is { } decision && decision.Find(Text(flag.Value)) is { } option)
            {
                decisions.SetValue(decision, option);
            }
        }

        AddSave(slot, episode, chapter, date);
    }

    public int LastFinished(SaveSlot slot) => slot.Metadata?.Find(StoryFiles.LastEpisodeFinished)?.Value switch
    {
        FloatValue number => (int)number.Value,
        IntValue number => number.Value,
        StringValue text when int.TryParse(text.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
        _ => 0,
    };

    public StoryChapter? GeneratedChapter(SaveSlot save, int episode)
    {
        if (save.Metadata?.GetString(StoryFiles.SavedScript) is not { } script || season.ChaptersOf(episode) is not { } chapters)
        {
            return null;
        }

        if (DialogLogSaves.FindRuntimeProperties(save, season.ScriptProperties)?.GetString(StoryCheckpointBuilder.PreviousScript) != StoryCheckpointBuilder.DeveloperMenuScript)
        {
            return save.FindFile(StoryFiles.LogicGameProperties) == null && script.Equals(chapters.Opening.Script, StringComparison.OrdinalIgnoreCase)
                ? chapters.Opening
                : null;
        }

        return chapters.Generated(script, DialogLogSaves.FindRuntimeProperties(save, StoryFiles.LogicGameProperties));
    }

    public StoryChapter? Chapter(SaveSlot save, int episode)
    {
        if (GeneratedChapter(save, episode) is { } generated)
        {
            return generated;
        }

        var chapterId = save.Metadata?.GetString(SaveMetadataKeys.ChapterId);
        if (string.IsNullOrEmpty(chapterId))
        {
            return null;
        }

        var script = save.Metadata?.GetString(StoryFiles.SavedScript);
        var chapters = season.ChaptersOf(episode)?.Chapters
            .Where(chapter => chapter.ChapterId == chapterId && chapter.Script.Equals(script, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];
        return chapters.Count == 1 ? chapters[0] : null;
    }

    private void Rewind(SaveSlot slot, int episode)
    {
        var metadata = slot.Metadata
            ?? throw new InvalidOperationException("Cannot set the resume point: the save has no slot metadata.");

        var log = new StoryEventLog(slot, season);
        log.Prepare();

        var decisions = new StoryDecisionLog(slot, season);
        var before = season.Decisions.Where(decision => decision.Episode < episode).ToList();
        var made = Made(decisions, before);

        foreach (var save in slot.Checkpoints.Where(save => (save.Metadata?.GetInt(SaveMetadataKeys.Episode) ?? season.LastEpisode) >= episode).ToList())
        {
            slot.Checkpoints.Remove(save);
            if (!slot.ObsoleteFileNames.Contains(save.FileName))
            {
                slot.ObsoleteFileNames.Add(save.FileName);
            }
        }

        log.TruncateFromEpisode(episode);
        for (var earlier = StorySeason.FirstEpisode; earlier < episode; earlier++)
        {
            log.FinishEpisode(earlier);
        }

        Restore(decisions, before, made);

        var latest = slot.Checkpoints.MaxBy(save => save.Metadata?.GetInt(SaveMetadataKeys.Serial) ?? 0);
        metadata.SetInt(SlotMetadataKeys.EpisodeInProgress, episode);
        metadata.SetString(SlotMetadataKeys.LatestSave, latest?.FileName ?? string.Empty);
        SetFinished(metadata, episode - 1);
        metadata.SetInt(StoryFiles.EpisodesCompleted, episode - 1);
        for (var number = StorySeason.FirstEpisode; number <= season.LastEpisode; number++)
        {
            if (number < episode)
            {
                metadata.SetBool(SlotMetadataKeys.CompletedEpisode(number), true);
            }
            else
            {
                metadata.Remove(Symbol.FromString(SlotMetadataKeys.CompletedEpisode(number)));
            }
        }

        if (metadata.Find(StoryFiles.EpisodesSkipped) != null)
        {
            metadata.SetString(StoryFiles.EpisodesSkipped, string.Empty);
        }

        if (metadata.Find(StoryFiles.GeneratedChoices) != null)
        {
            SetNumber(metadata, StoryFiles.GeneratedChoices, 0);
        }
    }

    private void AddSave(SaveSlot slot, int episode, StoryChapter chapter, string date)
    {
        var metadata = slot.Metadata!;
        var serial = (metadata.GetInt(SlotMetadataKeys.LatestSerial) ?? 0) + 1;
        var fileName = season.ChapterSaves ? DialogLogFiles.NextCheckpointName(slot) : DialogLogFiles.SaveName(slot.FileName, DialogLogFiles.AutosaveName);

        var save = _builder.Build(slot, episode, chapter, fileName, serial, date);
        slot.Checkpoints.RemoveAll(existing => existing.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        slot.Checkpoints.Add(save);
        slot.ObsoleteFileNames.RemoveAll(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        new StoryEventLog(slot, season).AppendSaveSerial(serial);

        metadata.SetString(SlotMetadataKeys.LatestSave, fileName);
        metadata.SetInt(SlotMetadataKeys.LatestSerial, serial);
    }

    private static SaveSlot? LatestSave(SaveSlot slot)
    {
        var latest = slot.Metadata?.GetString(SlotMetadataKeys.LatestSave);
        return string.IsNullOrEmpty(latest)
            ? null
            : slot.Checkpoints.FirstOrDefault(candidate => candidate.FileName.Equals(latest, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<StoryDecision, StoryDecisionOption> Made(StoryDecisionLog decisions, IEnumerable<StoryDecision> wanted) =>
        wanted.Where(decisions.IsSet).ToDictionary(decision => decision, decision => decisions.GetOption(decision)!);

    private static void Restore(StoryDecisionLog decisions, IReadOnlyList<StoryDecision> wanted, Dictionary<StoryDecision, StoryDecisionOption> made)
    {
        foreach (var (decision, option) in made)
        {
            if (!ReferenceEquals(decisions.GetOption(decision), option))
            {
                decisions.SetValue(decision, option);
            }
        }

        foreach (var decision in wanted.Where(decision => !made.ContainsKey(decision)))
        {
            if (decisions.GetOption(decision) == null)
            {
                decisions.SetValue(decision, decision.Options[0]);
            }
        }
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText(),
    };

    private void SetFinished(PropertySet metadata, int episode)
    {
        var stored = metadata.Find(StoryFiles.LastEpisodeFinished)?.Value;
        if (stored is StringValue || (stored == null && season.FinishedEpisodeKind == StoryNumberKind.Text))
        {
            metadata.SetString(StoryFiles.LastEpisodeFinished, episode.ToString(CultureInfo.InvariantCulture));
        }
        else if (stored == null && season.FinishedEpisodeKind == StoryNumberKind.Integer)
        {
            metadata.SetInt(StoryFiles.LastEpisodeFinished, episode);
        }
        else
        {
            SetNumber(metadata, StoryFiles.LastEpisodeFinished, episode);
        }
    }

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
