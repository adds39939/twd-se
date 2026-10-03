using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Checkpoints;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.S2.Chapters;
using TwdSaveEditor.Season.S2.Decisions;

namespace TwdSaveEditor.Season.S2.Saves;

public sealed class S2CheckpointBuilder : IS2CheckpointBuilder
{
    public const string DeveloperMenuScript = "DebugMenu";
    public const string PreviousScript = "Script - Previous";
    public const string SavedScript = "Saved Game Script";
    public const string ChapterId = "SaveLoad - Chapter ID";

    private const string SaveMetadataParent = "metadata_save_s2.prop";
    private const string AutoSave = "SaveLoad - Auto Save";
    private const string ScriptExtension = ".lua";
    private const uint LocalKeysFlag = 0x100;

    private static readonly string[] SharedResourceSets = ["MenuSeason2", "ProjectSeason2"];

    public SaveSlot Build(SaveSlot slot, StoryEpisodeChapters episode, StoryChapter chapter, string fileName, int serial, string date)
    {
        var episodeId = S2SlotFiles.EpisodeId(episode.Episode);

        var metadata = new PropertySet { Flags = LocalKeysFlag, ParentSymbols = [Symbol.FromString(SaveMetadataParent)] };
        metadata.SetInt(SaveMetadataKeys.Serial, serial);
        metadata.SetString(SaveMetadataKeys.Date, date);
        metadata.SetString(SaveMetadataKeys.Episode, episodeId);
        metadata.SetString(SaveMetadataKeys.ChapterId, chapter.ChapterId);
        metadata.SetString(SavedScript, chapter.Script);

        var sets = new SortedDictionary<ulong, PropertySet>();

        var saveLoad = CheckpointBundle.Runtime(sets, S2SlotFiles.SaveLoadProperties);
        saveLoad.SetBool(AutoSave, false);
        saveLoad.SetString(ChapterId, chapter.ChapterId);

        if (!chapter.StartsEpisode)
        {
            CheckpointBundle.Runtime(sets, S2SlotFiles.ScriptProperties).SetString(PreviousScript, DeveloperMenuScript);

            var game = CheckpointBundle.Runtime(sets, S2SlotFiles.LogicGameProperties);
            ApplyImportedChoices(slot, game);
            ApplyEarlierDecisions(slot, episode.Episode, game);
            foreach (var flag in chapter.Flags)
            {
                CheckpointBundle.Apply(game, flag.Key, flag.Value);
            }
        }

        var bundle = CheckpointBundle.Create(slot, fileName, metadata, chapter.Script + ScriptExtension, SharedResourceSets.Prepend(episodeId), sets);
        bundle.Modified = true;
        return bundle;
    }

    private static void ApplyImportedChoices(SaveSlot slot, PropertySet game)
    {
        foreach (var imported in S2ChapterCatalog.ImportedKeys)
        {
            foreach (var key in imported.Keys)
            {
                if (slot.Choices?.GetString(key) is not { } value)
                {
                    continue;
                }

                var name = $"Episode {imported.Episode} - {key}";
                if (bool.TryParse(value, out var flag))
                {
                    game.SetBool(name, flag);
                }
                else
                {
                    game.SetString(name, value);
                }
            }
        }
    }

    private static void ApplyEarlierDecisions(SaveSlot slot, int episode, PropertySet game)
    {
        var log = new S2EventLogEditor(slot);
        foreach (var decision in S2DecisionCatalog.All.Where(decision => decision.Episode < episode && decision.LogicKey != null))
        {
            var option = log.FindSeen(decision);
            if (decision.LogicIsText)
            {
                game.SetString(decision.LogicKey!, option?.LogicValue ?? string.Empty);
            }
            else
            {
                game.SetBool(decision.LogicKey!, option?.LogicValue != null);
            }
        }
    }
}
