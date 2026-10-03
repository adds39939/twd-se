using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S2.Chapters;

namespace TwdSaveEditor.Season.S2.Saves;

public interface IS2CheckpointBuilder
{
    SaveSlot Build(SaveSlot slot, S2EpisodeChapters episode, S2Chapter chapter, string fileName, int serial, string date);
}
