using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IInventoryHandler
{
    IReadOnlyList<string> InventoryNotes { get; }

    InventoryState GetInventory(SaveSlot slot);

    void SetInventory(SaveSlot slot, IReadOnlyList<string> itemIds);

    IReadOnlyList<string> GetCarriedItems(SaveSlot slot);
}
