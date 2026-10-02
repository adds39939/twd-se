using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogBranch(
    ulong Id,
    string Kind,
    string Group,
    ulong Name,
    ulong First,
    DialogRule? Visibility,
    string VisibilityScript,
    IReadOnlyList<DialogRule> Conditions,
    MetaPropertySet? UserProps);
