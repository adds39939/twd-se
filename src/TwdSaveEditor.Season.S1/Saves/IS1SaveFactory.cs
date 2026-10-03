using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S1.Saves;

public interface IS1SaveFactory
{
    SaveSlot Create(string fileName, int episode);
}
