using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Tools.BuildCheckpoint.Checkpoints;

public sealed class CheckpointCommands(string outputDirectory, TextWriter output)
{
    public void Strip(string slotPath, string autosavePath, int slotNumber, bool keepAgents, IReadOnlyList<ulong> extraFiles)
    {
        var slot = Slot(slotPath, slotNumber);
        var autosave = BundleReader.Read(autosavePath);
        Save(slot, CheckpointBuilder.Strip(autosave, S1SlotFiles.AutosaveName(slot.FileName), keepAgents, extraFiles));
    }

    public void Generate(string slotPath, string referencePath, int slotNumber, bool withEpisodeFlags, string date)
    {
        var slot = Slot(slotPath, slotNumber);
        var reference = BundleReader.Read(referencePath);
        var checkpoint = CheckpointBuilder.Describe(reference);
        var flags = withEpisodeFlags ? CheckpointBuilder.EpisodeFlags(reference) : null;

        output.WriteLine($"{checkpoint.EpisodeId} {checkpoint.Script} chapter={checkpoint.ChapterId} item={checkpoint.DialogItem} flags={flags?.AllProperties.Count() ?? 0}");
        Save(slot, CheckpointBuilder.Generate(slot, S1SlotFiles.AutosaveName(slot.FileName), checkpoint, date, flags));
    }

    public void Chapter(string slotPath, int slotNumber, int episode, string chapterId, IEnumerable<string> choices, string date)
    {
        var slot = Slot(slotPath, slotNumber);
        var accessor = new S1ChoiceAccessor(slot);
        foreach (var choice in choices)
        {
            var separator = choice.LastIndexOf('=');
            accessor.SetChoiceValue(choice[..separator], choice[(separator + 1)..]);
        }

        S1ResumePoint.RestartFromChapter(slot, episode, chapterId, date);
        if (slot.Autosave == null)
        {
            output.WriteLine($"{chapterId} starts episode {episode}: no checkpoint is needed.");
            Save(slot);
            return;
        }

        var save = SaveGameCodec.Read(slot.Autosave.FindFile(BundleFileNames.SaveGame)!.Data);
        output.WriteLine($"episode {episode} {chapterId}: {save.LuaDoFile}, {save.RuntimePropertyNames.Count} property sets");
        Save(slot, slot.Autosave);
    }

    private static SaveSlot Slot(string slotPath, int number)
    {
        var name = $"wd1_saveslot{number}.bundle";
        var slot = BundleReader.Read(File.ReadAllBytes(slotPath), name);
        slot.Metadata!.SetString(SlotMetadataKeys.LatestSave, S1SlotFiles.AutosaveName(name));
        return slot;
    }

    private void Save(params SaveSlot[] bundles)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (var bundle in bundles)
        {
            var bytes = BundleWriter.Write(bundle);
            File.WriteAllBytes(Path.Combine(outputDirectory, bundle.FileName), bytes);
            output.WriteLine($"{bundle.FileName}: {bundle.Files.Count} files, {bytes.Length} bytes");
        }
    }
}
