namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogRule(DialogLogicGroup Conditions, DialogLogicGroup Actions, DialogLogicGroup Otherwise)
{
    public bool IsEmpty => Conditions.IsEmpty && Actions.IsEmpty && Otherwise.IsEmpty;
}
