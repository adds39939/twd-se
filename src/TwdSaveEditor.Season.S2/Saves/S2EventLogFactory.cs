using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.S2.Saves;

public static class S2EventLogFactory
{
    public static EventLog Create(string slotFileName) => DialogLogFiles.NewLog(slotFileName);
}
