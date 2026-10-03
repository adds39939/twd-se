using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Season.S1.Inventory;

namespace TwdSaveEditor.Season.S1.Saves;

public sealed class S1CheckpointRefresher(IS1ResumePoint resume, IS1CheckpointBuilder builder, IS1Inventory inventory) : IS1CheckpointRefresher
{
    private const int GeneratedSerial = 1;

    public void Refresh(SaveSlot slot)
    {
        if (resume.GeneratedChapter(slot) is not { } chapter || slot.Autosave?.Metadata is not { } saved)
        {
            return;
        }

        var episode = S1SlotFiles.EpisodeNumber(saved.GetString(SaveMetadataKeys.Episode));
        if (episode == null || S1ChapterCatalog.ForEpisode(episode.Value) is not { } chapters)
        {
            return;
        }

        var held = inventory.GetState(slot);
        slot.Autosave = builder.Build(slot, chapters, chapter,
            saved.GetInt(SaveMetadataKeys.Serial) ?? GeneratedSerial, saved.GetString(SaveMetadataKeys.Date) ?? string.Empty);
        if (held.Editable)
        {
            inventory.SetItems(slot, held.Held, [.. chapters.DecisionFlags.Select(flag => flag.Key)]);
        }
    }
}
