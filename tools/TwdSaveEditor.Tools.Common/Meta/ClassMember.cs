namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record ClassMember(string Name, string Type, int Flags)
{
    private const int SerializeDisabled = 0x1;

    public bool IsSerialized => (Flags & SerializeDisabled) == 0;
}
