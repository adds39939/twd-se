using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Checkpoints;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryCheckpointBuilder(StorySeason season)
{
    public const string DeveloperMenuScript = "DebugMenu";
    public const string PreviousScript = "Script - Previous";
    public const string ChapterId = "SaveLoad - Chapter ID";
    public const string CheckpointDialogFile = "SaveLoad - Checkpoint Dialog File";
    public const string CheckpointDialogNode = "SaveLoad - Checkpoint Dialog Node";

    private const string AutoSave = "SaveLoad - Auto Save";
    private const string RuntimeVisible = "Runtime: Visible";
    private const string ScriptExtension = ".lua";
    private const uint LocalKeysFlag = 0x100;

    public SaveSlot Build(SaveSlot slot, int episode, StoryChapter chapter, string fileName, int serial, string date)
    {
        var project = season.ProjectName(episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(season.SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetInt(SaveMetadataKeys.Episode, episode);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.ChapterId, chapter.ChapterId);
        metadata.SetString(StoryFiles.SavedScript, chapter.Script);
        metadata.SetString(StoryFiles.SavedProject, project);

        var sets = new SortedDictionary<ulong, PropertySet>();
        var saveLoad = CheckpointBundle.Runtime(sets, season.SaveLoadProperties);
        saveLoad.SetBool(AutoSave, false);
        if (season.ChapterSaves)
        {
            saveLoad.SetString(ChapterId, chapter.ChapterId);
        }

        if (chapter.Dialog != null && chapter.DialogNode != null)
        {
            saveLoad.SetString(CheckpointDialogFile, chapter.Dialog);
            saveLoad.SetString(CheckpointDialogNode, chapter.DialogNode);
        }

        if (!chapter.StartsEpisode)
        {
            CheckpointBundle.Runtime(sets, season.ScriptProperties).SetString(PreviousScript, DeveloperMenuScript);

            var game = CheckpointBundle.Runtime(sets, StoryFiles.LogicGameProperties);
            if (season.GameLogicVisible)
            {
                game.SetBool(RuntimeVisible, true);
            }

            ApplyLogicKeys(slot, episode, game);
            foreach (var flag in chapter.Flags)
            {
                CheckpointBundle.Apply(game, flag.Key, flag.Value);
            }
        }

        var bundle = CheckpointBundle.Create(slot, fileName, metadata, chapter.Script + ScriptExtension, season.SharedResourceSets.Prepend(project), sets);
        bundle.Modified = true;
        return bundle;
    }

    public void ApplyLogicKeys(SaveSlot slot, int episode, PropertySet game)
    {
        var nodes = new StoryEventLog(slot, season).Nodes();
        foreach (var key in season.LogicKeys.Where(key => key.ReadFrom <= episode))
        {
            Set(game, key, nodes);
        }
    }

    public static void Set(PropertySet game, StoryLogicKey key, IReadOnlySet<ulong> nodes)
    {
        switch (StoryDecisionLog.Evaluate(key, nodes))
        {
            case bool flag:
                game.SetBool(key.Key, flag);
                break;
            case string text:
                game.SetString(key.Key, text);
                break;
        }
    }
}
