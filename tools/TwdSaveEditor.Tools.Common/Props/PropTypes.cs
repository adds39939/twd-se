namespace TwdSaveEditor.Tools.Common.Props;

public static class PropTypes
{
    public const ulong String = 0xCD9C6E605F5AF4B4;
    public const ulong Bool = 0x9004C5587575D6C0;
    public const ulong Int32 = 0x7CACEEBCD26D075C;
    public const ulong PropertySet = 0x00000000000002AB;
    public const ulong Container = 0xCD75DC4F6B9F15D2;

    public static bool IsKnown(ulong type) => type is String or Bool or Int32 or PropertySet or Container;

    public static bool IsNested(ulong type) => type is PropertySet or Container;
}
