using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2EventLogFactory
{
    public static EventLog Create(string slotFileName)
    {
        var name = S2SlotFiles.StorageName(slotFileName);
        var storage = new EventLogStorage
        {
            SessionId = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = name,
        };

        return new EventLog(name, storage) { StorageModified = true };
    }
}
