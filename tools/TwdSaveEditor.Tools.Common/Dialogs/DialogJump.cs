namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogJump(ulong Target, ulong TargetName, int TargetClass, int Behaviour, ulong Dialog)
{
    public const int ToName = 1;
    public const int ToParent = 2;
    public const int ToNodeAfterParentWait = 3;

    public const int JumpAndExecute = 1;
    public const int JumpExecuteAndReturn = 2;
    public const int Return = 3;
}
