using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Persistence;

namespace TwdSaveEditor.Season.S1.Saves;

public static class S1ResumePoint
{
    public const int ExtraEpisode = 6;

    private const int FirstEpisode = 1;
    private const int GeneratedSerial = 1;
    private const int MaxGeneratedFiles = 16;
    private const int EpisodeBase = 100;

    public static ResumeState GetState(SaveSlot slot)
    {
        var metadata = slot.Metadata;
        var progress = Math.Clamp(metadata?.GetInt(SlotMetadataKeys.Progress) ?? FirstEpisode, FirstEpisode, ExtraEpisode);

        if (string.IsNullOrEmpty(metadata?.GetString(SlotMetadataKeys.LatestSave)))
            return new ResumeState(progress, null, null);

        var saved = slot.Autosave?.Metadata;
        if (saved == null)
            return new ResumeState(progress, null, null, slot.AutosaveDamaged);

        var episode = S1SlotFiles.EpisodeNumber(saved.GetString(SaveMetadataKeys.Episode)) ?? progress;
        var checkpoint = GeneratedChapter(slot)?.Title ?? saved.GetString(SaveMetadataKeys.ChapterId) ?? string.Empty;
        return new ResumeState(episode, checkpoint, saved.GetString(SaveMetadataKeys.Date), slot.AutosaveDamaged);
    }

    public static IReadOnlyList<ChapterInfo> GetChapters(int episode) =>
        S1ChapterCatalog.ForEpisode(episode)?.Chapters.Select(chapter => new ChapterInfo(chapter.Id, chapter.Title, chapter.Group)).ToList() ?? [];

    public static void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date)
    {
        var chapters = S1ChapterCatalog.ForEpisode(episode)
            ?? throw new ArgumentException($"Episode {episode} has no chapter list.", nameof(episode));
        var chapter = chapters.Find(chapterId)
            ?? throw new ArgumentException($"Episode {episode} has no chapter {chapterId}.", nameof(chapterId));

        RestartFromEpisode(slot, episode);
        if (chapter.StartsEpisode)
            return;

        var metadata = slot.Metadata!;
        var autosave = S1SlotFiles.AutosaveName(slot.FileName);
        metadata.SetInt(SlotMetadataKeys.LatestSerial, GeneratedSerial);
        metadata.SetString(SlotMetadataKeys.LatestSave, autosave);

        slot.Autosave = S1CheckpointBuilder.Build(slot, chapters, chapter, GeneratedSerial, date);
        slot.ObsoleteFileNames.Remove(autosave);
    }

    public static void RefreshGeneratedCheckpoint(SaveSlot slot)
    {
        if (GeneratedChapter(slot) is not { } chapter || slot.Autosave?.Metadata is not { } saved)
            return;

        var episode = S1SlotFiles.EpisodeNumber(saved.GetString(SaveMetadataKeys.Episode));
        if (episode == null || S1ChapterCatalog.ForEpisode(episode.Value) is not { } chapters)
            return;

        slot.Autosave = S1CheckpointBuilder.Build(slot, chapters, chapter,
            saved.GetInt(SaveMetadataKeys.Serial) ?? GeneratedSerial, saved.GetString(SaveMetadataKeys.Date) ?? string.Empty);
    }

    private static S1Chapter? GeneratedChapter(SaveSlot slot)
    {
        if (slot.Autosave?.Metadata is not { } saved || slot.Autosave.Files.Count > MaxGeneratedFiles)
            return null;

        var episode = S1SlotFiles.EpisodeNumber(saved.GetString(SaveMetadataKeys.Episode));
        var chapterId = saved.GetString(SaveMetadataKeys.ChapterId);
        return episode == null || chapterId == null ? null : S1ChapterCatalog.ForEpisode(episode.Value)?.Find(chapterId);
    }

    public static void RestartFromEpisode(SaveSlot slot, int episode)
    {
        var metadata = slot.Metadata
            ?? throw new InvalidOperationException("Cannot set the resume point: the save has no slot metadata.");

        episode = Math.Clamp(episode, FirstEpisode, ExtraEpisode);

        metadata.SetInt(SlotMetadataKeys.LatestSerial, 0);
        metadata.SetInt(SlotMetadataKeys.Progress, episode);
        metadata.SetString(SlotMetadataKeys.EpisodeInProgress, S1SlotFiles.EpisodeId(episode));
        metadata.SetString(SlotMetadataKeys.LatestSave, string.Empty);

        for (var number = FirstEpisode; number <= ExtraEpisode; number++)
        {
            var key = SlotMetadataKeys.CompletedEpisode(number);
            if (number >= episode)
                metadata.SetBool(key, false);
            else if (episode != ExtraEpisode || metadata.GetBool(key) == null)
                metadata.SetBool(key, episode != ExtraEpisode);
        }

        FillMissingChoices(slot, EpisodeBase + episode);

        slot.Autosave = null;
        slot.AutosaveDamaged = false;
        var autosave = S1SlotFiles.AutosaveName(slot.FileName);
        if (!slot.ObsoleteFileNames.Contains(autosave))
            slot.ObsoleteFileNames.Add(autosave);
    }

    private static void FillMissingChoices(SaveSlot slot, int persistentEpisode)
    {
        var accessor = new S1ChoiceAccessor(slot);
        foreach (var choice in S1ChoiceCatalog.Before(persistentEpisode))
        {
            if (accessor.GetChoiceValue(choice.ChoiceKey) == null)
                accessor.ApplyChoice(choice, 0);
        }
    }
}
