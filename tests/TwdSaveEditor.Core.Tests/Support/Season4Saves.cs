using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Integration;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Core.Tests.Support;

internal static class Season4Saves
{
    public const string Slot = "wd4_saveslot2.bundle";
    public const string Autosave = "_wd4_saveslot2_autosave.bundle";
    public const string Storage = "_wd4_saveslot2_id.estore";

    public static readonly string[] Pages =
    [
        "_wd4_saveslot2_id_Page734.epage",
        "_wd4_saveslot2_id_Page17124.epage",
        "_wd4_saveslot2_id_Page18120.epage",
        "_wd4_saveslot2_id_Page19079.epage",
    ];

    public static readonly S4Handler Handler = new();

    public static byte[] ReadBytes(string fileName) => File.ReadAllBytes(TestDataHelper.GetPath("S4", fileName));

    public static SaveSlot LoadEpisode1Save()
    {
        var files = Handler.FindCompanionFiles(Slot, [Slot, Autosave, Storage, .. Pages])
            .Select(name => new CompanionFile(name, ReadBytes(name)))
            .ToList();
        return Load(ReadBytes(Slot), Slot, files);
    }

    public static SaveSlot Reload(SaveSlot slot)
    {
        var written = Handler.BuildCompanionFiles(slot).ToDictionary(file => file.Name, file => file.Data, StringComparer.OrdinalIgnoreCase);
        var files = Handler.GetCompanionFileNames(slot)
            .Select(name => new CompanionFile(name, written.GetValueOrDefault(name) ?? ReadBytes(name)))
            .ToList();
        return Load(BundleWriter.Write(slot), slot.FileName, files);
    }

    public static IChoiceAccessor Accessor(SaveSlot slot) => Handler.CreateChoiceAccessor(slot)!;

    private static SaveSlot Load(byte[] slotBytes, string name, IReadOnlyList<CompanionFile> files)
    {
        var slot = BundleReader.Read(slotBytes, name);
        slot.DetectedSeasonKey = Handler.SeasonKey;
        Handler.AttachCompanionFiles(slot, files);
        return slot;
    }
}
