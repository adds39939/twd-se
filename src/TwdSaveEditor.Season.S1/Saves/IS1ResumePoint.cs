using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Chapters;

namespace TwdSaveEditor.Season.S1.Saves;

public interface IS1ResumePoint
{
    ResumeState GetState(SaveSlot slot);

    IReadOnlyList<ChapterInfo> GetChapters(int episode);

    S1Chapter? GeneratedChapter(SaveSlot slot);

    void RestartFromEpisode(SaveSlot slot, int episode);

    void RestartFromChapter(SaveSlot slot, int episode, string chapterId, string date);
}
