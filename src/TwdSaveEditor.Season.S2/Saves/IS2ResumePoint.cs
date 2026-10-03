using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S2.Chapters;

namespace TwdSaveEditor.Season.S2.Saves;

public interface IS2ResumePoint
{
    ResumeState GetState(SaveSlot slot);

    SaveSlot? ResumeSave(SaveSlot slot);

    S2Chapter? Chapter(SaveSlot save, int episode);

    IReadOnlyList<ChapterInfo> GetChapters(int episode);

    void RestartFromEpisode(SaveSlot slot, int episode, string date);

    void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date);

    PropertySet? Properties(SaveSlot save, ulong name);
}
