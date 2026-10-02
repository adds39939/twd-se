using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Tests.Common.Saves;

public static class Season2Saves
{
    public const string Slot = "wd2_saveslot1.bundle";
    public const string Autosave = "_wd2_saveslot1_autosave.bundle";
    public const string Storage = "_wd2_saveslot1_id.estore";

    public static readonly string[] Pages =
    [
        "_wd2_saveslot1_id_Page913.epage",
        "_wd2_saveslot1_id_Page1897.epage",
        "_wd2_saveslot1_id_Page2734.epage",
    ];

    public static readonly S2Handler Handler = new();

    public static IEnumerable<string> CompanionNames => [Autosave, Storage, .. Pages];

    public static byte[] ReadBytes(string fileName) => File.ReadAllBytes(TestDataHelper.GetPath("S2", fileName));

    public static SaveSlot LoadEpisode1Save()
    {
        var files = Handler.FindCompanionFiles(Slot, [Slot, .. CompanionNames])
            .Select(name => new CompanionFile(name, ReadBytes(name)))
            .ToList();
        return Load(ReadBytes(Slot), files);
    }

    public static SaveSlot Reload(SaveSlot slot)
    {
        var written = Handler.BuildCompanionFiles(slot).ToDictionary(file => file.Name, file => file.Data, StringComparer.OrdinalIgnoreCase);
        var files = Handler.GetCompanionFileNames(slot)
            .Select(name => new CompanionFile(name, written.GetValueOrDefault(name) ?? ReadBytes(name)))
            .ToList();
        return Load(BundleWriter.Write(slot), files);
    }

    public static IChoiceAccessor Accessor(SaveSlot slot) => Handler.CreateChoiceAccessor(slot)!;

    private static SaveSlot Load(byte[] slotBytes, IReadOnlyList<CompanionFile> files)
    {
        var slot = BundleReader.Read(slotBytes, Slot);
        slot.DetectedSeasonKey = Handler.SeasonKey;
        Handler.AttachCompanionFiles(slot, files);
        return slot;
    }
}
