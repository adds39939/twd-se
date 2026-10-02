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
    public static readonly ulong ResourceBundle = TelltaleHash.ComputeCrc64("ResourceBundle");
    public static readonly ulong SaveGame = TelltaleHash.ComputeCrc64("SaveGame");
    public static readonly ulong SaveGameAgentInfo = TelltaleHash.ComputeCrc64("SaveGame::AgentInfo");
    public static readonly ulong DialogHandle = TelltaleHash.ComputeCrc64("Handle<Dlg>");

    public const ulong ChoicesContainer = 0x8AD17AD4CB809956;
}
