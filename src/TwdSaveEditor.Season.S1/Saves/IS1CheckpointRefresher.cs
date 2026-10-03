using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S1.Saves;

public interface IS1CheckpointRefresher
{
    void Refresh(SaveSlot slot);
}
