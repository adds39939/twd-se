using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogNode(
    ulong Id,
    string Kind,
    ulong Name,
    uint Flags,
    ulong Previous,
    ulong Next,
    DialogRule? Visibility,
    string VisibilityScript,
    MetaPropertySet? UserProps,
    IReadOnlyList<DialogBranch> Branches,
    DialogRule? Rule,
    string? Script,
    DialogJump? Jump,
    ulong Chore,
    MetaObject Source)
{
    public string? UserText(string key) => (UserProps?.Find(key) as MetaScalar)?.Text;
}
