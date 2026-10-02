using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogLogicEntry(string Target, ulong Key, string Type, MetaNode Value, bool Negate, int Comparison, int Action);
