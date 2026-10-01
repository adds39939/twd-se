using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Integration;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Handlers;

namespace TwdSaveEditor.Core.Tests.Support;

internal static class Season1Saves
{
    public const string Slot = "wd1_saveslot2.bundle";
    public const string Autosave = "_wd1_saveslot2_autosave.bundle";
    public const ulong LogicGameProperties = 0x1D3802238E8CE045;

    public static readonly S1Handler Handler = new();

    public static byte[] ReadBytes(string fileName) => File.ReadAllBytes(TestDataHelper.GetPath("S1", fileName));

    public static SaveSlot LoadEpisode4Save()
    {
        var slot = BundleReader.Read(ReadBytes(Slot), Slot);
        slot.DetectedSeasonKey = Handler.SeasonKey;
        Handler.AttachCompanionFiles(slot, [new CompanionFile(Autosave, ReadBytes(Autosave))]);
        return slot;
    }

    public static SaveSlot Reload(SaveSlot slot)
    {
        var reloaded = BundleReader.Read(BundleWriter.Write(slot), slot.FileName);
        reloaded.DetectedSeasonKey = Handler.SeasonKey;
        Handler.AttachCompanionFiles(reloaded, Handler.BuildCompanionFiles(slot));
        return reloaded;
    }

    public static IChoiceAccessor Accessor(SaveSlot slot) => Handler.CreateChoiceAccessor(slot)!;

    public static PropertySet LogicGame(SaveSlot slot)
    {
        var file = slot.Autosave!.FindFile(LogicGameProperties)!;
        Assert.True(BundleReader.TryParseProperties(file));
        return file.Properties!;
    }
}
