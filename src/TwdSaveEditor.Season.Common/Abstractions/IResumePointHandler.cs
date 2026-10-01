using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IResumePointHandler
{
    IReadOnlyList<EpisodeInfo> ResumeEpisodes { get; }

    ResumeState GetResumeState(SaveSlot slot);

    void RestartFromEpisode(SaveSlot slot, int episode);
}
