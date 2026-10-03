using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Chapters;

namespace TwdSaveEditor.Season.S1.Saves;

public interface IS1CheckpointBuilder
{
    SaveSlot Build(SaveSlot slot, S1EpisodeChapters episode, S1Chapter chapter, int serial, string date);
}
