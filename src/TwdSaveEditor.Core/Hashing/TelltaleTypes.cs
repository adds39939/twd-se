namespace TwdSaveEditor.Core.Hashing;

public static class TelltaleTypes
{
    public static readonly ulong Bool = TelltaleHash.ComputeCrc64("bool");
    public static readonly ulong Int32 = TelltaleHash.ComputeCrc64("int32");
    public static readonly ulong Float = TelltaleHash.ComputeCrc64("float");
    public static readonly ulong String = TelltaleHash.ComputeCrc64("String");
    public static readonly ulong Symbol = TelltaleHash.ComputeCrc64("Symbol");
    public static readonly ulong Flags = TelltaleHash.ComputeCrc64("Flags");
    public static readonly ulong PropertySet = TelltaleHash.ComputeCrc64("PropertySet");

    public const ulong ChoicesContainer = 0x8AD17AD4CB809956;
}
