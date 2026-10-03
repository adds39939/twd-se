using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S4.Collectibles;

public interface IS4Collectibles
{
    InventoryState GetState(SaveSlot slot);

    void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held);
}
