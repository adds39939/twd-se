using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.S3.Saves;

public static class S3SlotFiles
{
    public static readonly ulong InventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory");
    public static readonly ulong OwnerInventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory_Javier");
}
