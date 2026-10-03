using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Tests.Common.Saves;

public static class Season1Saves
{
    public const string Slot = "wd1_saveslot2.bundle";
    public const string Autosave = "_wd1_saveslot2_autosave.bundle";
    public const ulong LogicGameProperties = 0x1D3802238E8CE045;

    public static readonly S1Handler Handler = TestSeasons.Handler<S1Handler>("s1");

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
        return BundleReader.TryParseProperties(file) ? file.Properties! : throw new InvalidDataException("The autosave's game logic cannot be read.");
    }
}
