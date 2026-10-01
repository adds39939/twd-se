using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Persistence;

namespace TwdSaveEditor.Season.S1.Saves;

public static class S1ResumePoint
{
    public const int ExtraEpisode = 6;

    private const int FirstEpisode = 1;
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
        return new ResumeState(episode, saved.GetString(SaveMetadataKeys.ChapterId) ?? string.Empty,
            saved.GetString(SaveMetadataKeys.Date), slot.AutosaveDamaged);
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
