using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Season.S1.Saves;

public static class S1RuntimeProperties
{
    public const string LogicScene = "module_logic.scene";
    public const string GameLogicAgent = "logic_game";
    public const string CheckpointAgent = "logic_checkpoint";
    public const string SaveLoadAgent = "logic_saveload";

    public static ulong Name(string agent, string scene) => TelltaleHash.ComputeCrc64($"\"{agent}:{scene}\" Runtime Properties");

    public static ulong LogicName(string agent) => Name(agent, LogicScene);
}
