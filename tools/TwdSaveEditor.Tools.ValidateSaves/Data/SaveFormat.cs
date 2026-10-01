using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.ValidateSaves.Data;

public static class SaveFormat
{
    public const ulong ChoicesContainer = 0x8AD17AD4CB809956;

    public static readonly OrderedDictionary<ulong, string> KnownMetadataHashes = new()
    {
        [0x7C725227A47FD1BA] = "chapter count int",
        [0x94C245DACB1ADDC3] = "save slot index int",
        [0x4F8338150CC8BCD6] = "bool property",
        [0xB218E7C003A67CE9] = "episode id string",
        [0xF235E9FCE9562E01] = "autosave bundle string",
    };

    public static readonly VersionEntry[] OuterVersionEntries =
    [
        new(0xE09B099B8076C147, 0x5A585C97),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    public static readonly VersionEntry[] InnerVersionEntries =
    [
        new(0xCD75DC4F6B9F15D2, 0x21F2BCC9),
        new(0x84283CB979D71641, 0x0527D6BF),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    public static readonly Dictionary<string, (ulong First, ulong Second)> FileHashes = new()
    {
        ["metadata_slot.p"] = (0xEE382691929657D5, 0xCD75DC4F6B9F15D2),
        ["choices.prop"] = (0x819F96241D349414, 0xCD75DC4F6B9F15D2),
        ["choicestats.pro"] = (0xBD8881F09F440467, 0xCD75DC4F6B9F15D2),
    };

    public static readonly (string Name, ulong Hash)[] EventTypes =
    [
        ("Executing Dialog Node", EventLogFormat.ExecutingDialogNode),
        ("Dialog Choice", EventLogFormat.DialogChoice),
        ("Begin Episode", EventLogFormat.BeginEpisode),
        ("End Episode", EventLogFormat.EndEpisode),
        ("Save Serial", EventLogFormat.SaveSerial),
    ];

    public static string EventTypeName(ulong hash)
    {
        var name = EventTypes.FirstOrDefault(type => type.Hash == hash).Name;
        return name == null ? "Unknown" : name.Replace(" ", "");
    }
}
