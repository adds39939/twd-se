using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.S3.Saves;

public static class S3SlotFiles
{
    public const int FirstEpisode = 1;
    public const int LastEpisode = 5;

    public const string LastEpisodeFinished = "Last Episode Finished";
    public const string EpisodesCompleted = "Episodes Completed";
    public const string EpisodesSkipped = "Episodes Skipped";
    public const string GeneratedChoices = "Generated Choices ID";
    public const string SavedProject = "Saved Game Project";
    public const string SavedScript = "Saved Game Script";

    public static readonly ulong LogicGameProperties = DialogLogFiles.RuntimeProperties("logic_game");
    public static readonly ulong SaveLoadProperties = DialogLogFiles.RuntimeProperties("logic_saveload");
    public static readonly ulong ScriptProperties = DialogLogFiles.RuntimeProperties("logic_script");
    public static readonly ulong InventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory");
    public static readonly ulong OwnerInventoryProperties = DialogLogFiles.RuntimeProperties("logic_inventory_Javier");

    public static string ProjectName(int episode) => $"WalkingDead30{episode}";

    public static string AutosaveName(string slotFileName) => DialogLogFiles.SaveName(slotFileName, DialogLogFiles.AutosaveName);
}
