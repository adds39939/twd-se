using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S2.Saves;

public interface IS2ResumePoint
{
    ResumeState GetState(SaveSlot slot);

    SaveSlot? ResumeSave(SaveSlot slot);

    StoryChapter? Chapter(SaveSlot save, int episode);

    IReadOnlyList<ChapterInfo> GetChapters(int episode);

    void RestartFromEpisode(SaveSlot slot, int episode, string date);

    void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date);
}
