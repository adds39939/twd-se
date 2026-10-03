using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S2.Saves;

public interface IS2SaveFactory
{
    SaveSlot Create(string fileName, string episodeId);
}
