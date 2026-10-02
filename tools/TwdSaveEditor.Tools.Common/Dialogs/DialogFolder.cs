namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogFolder(ulong Id, ulong Name, IReadOnlyList<DialogBranch> Children);
