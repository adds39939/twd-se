using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S2.Saves;

public interface IS2CheckpointBuilder
{
    SaveSlot Build(SaveSlot slot, StoryEpisodeChapters episode, StoryChapter chapter, string fileName, int serial, string date);
}
