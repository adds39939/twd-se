using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Story;

public static class StoryFiles
{
    public const string LastEpisodeFinished = "Last Episode Finished";
    public const string EpisodesCompleted = "Episodes Completed";
    public const string EpisodesSkipped = "Episodes Skipped";
    public const string GeneratedChoices = "Generated Choices ID";
    public const string SavedProject = "Saved Game Project";
    public const string SavedScript = "Saved Game Script";
    public const string CheckpointName = "checkpoint";

    public static readonly ulong LogicGameProperties = DialogLogFiles.RuntimeProperties("logic_game");
    public static readonly ulong SaveLoadProperties = DialogLogFiles.RuntimeProperties("logic_saveload");
    public static readonly ulong ScriptProperties = DialogLogFiles.RuntimeProperties("logic_script");
}
