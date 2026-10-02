namespace TwdSaveEditor.Tools.BuildCheckpoint.Checkpoints;

public static class LogicProperties
{
    public const ulong Game = 0x1D3802238E8CE045;
    public const ulong Checkpoint = 0xBFA39BD9297F70DA;
    public const ulong SaveLoad = 0xBFC806883BAFB11F;
    public const ulong Script = 0xFDA9856B5C32F67B;

    public static readonly ulong[] All = [Game, Checkpoint, SaveLoad, Script];
}
