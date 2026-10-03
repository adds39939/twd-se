using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.Story;

public interface IStorySaveFactory
{
    SaveSlot Create(string fileName, int episode, StorySeason season);
}
