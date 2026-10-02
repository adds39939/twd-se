using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S3.Saves;

public static class S3SlotFiles
{
    public static readonly ulong InventoryProperties = StoryFiles.InventoryProperties;
    public static readonly ulong OwnerInventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory_Javier");
}
