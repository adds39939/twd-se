using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S2.Inventory;

public interface IS2Inventory
{
    InventoryState GetState(SaveSlot slot);

    void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held);

    IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot);
}
