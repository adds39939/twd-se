using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S1.Saves;

public static class S1RuntimeProperties
{
    public const string LogicScene = "module_logic.scene";
    public const string GameLogicAgent = "logic_game";
    public const string CheckpointAgent = "logic_checkpoint";
    public const string SaveLoadAgent = "logic_saveload";

    private const string VisibilityProxy = "Transient Visibility Proxy";
    private const string RuntimeVisible = "Runtime: Visible";
    private const uint RuntimeFlag = 0x10;

    public static ulong Name(string agent, string scene) => TelltaleHash.ComputeCrc64($"\"{agent}:{scene}\" Runtime Properties");

    public static ulong LogicName(string agent) => Name(agent, LogicScene);

    public static PropertySet Create(bool visible)
    {
        var properties = new PropertySet { Flags = RuntimeFlag };
        properties.SetInt(VisibilityProxy, -1);
        properties.SetBool(RuntimeVisible, visible);
        return properties;
    }
}
