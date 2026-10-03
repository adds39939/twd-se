using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S1.Inventory;

public interface IS1Inventory
{
    InventoryState GetState(SaveSlot slot);

    void SetItems(SaveSlot slot, IReadOnlyList<HeldItem> held, IReadOnlyCollection<string>? untouched = null);

    IReadOnlyList<HeldItem> CarriedItems(SaveSlot slot);
}
