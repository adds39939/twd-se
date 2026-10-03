using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ValidateSaves.Data;

namespace TwdSaveEditor.Tools.ValidateSaves.Validators;

public static partial class ChoiceFormatValidator
{
    public static List<string> Validate(SeasonInfo season)
    {
        var results = new List<string>();

        switch (season.ChoiceFormat)
        {
            case ChoiceFormat.ChoicesProp:
                AddChoicesPropResults(results, season);
                break;
            case ChoiceFormat.EventLog:
                AddEventLogResults(results);
                break;
            case ChoiceFormat.ChoiceStats:
                AddChoiceStatsResults(results);
                break;
        }

        return results;
    }

    private static void AddChoicesPropResults(List<string> results, SeasonInfo season)
    {
        results.Add("  Format: ChoicesContainer PropertySet in choices.prop/season1.prop");
        results.Add("  Serialization: u32(count) + count x (u32(strlen) + chars + u8(bool))");
        results.Add($"  Type hash: 0x{SaveFormat.ChoicesContainer:X16} (ChoicesContainer)");

        var bundlePath = Path.Combine(ToolPaths.TestData, season.Key, season.TestBundle);
        if (!File.Exists(bundlePath))
        {
            return;
        }

        var index = Bytes.IndexOf(File.ReadAllBytes(bundlePath), Bytes.FromU64(SaveFormat.ChoicesContainer));
        results.Add(index >= 0
            ? $"  MATCH: ChoicesContainer hash 0x{SaveFormat.ChoicesContainer:X16} found at offset 0x{index:X}"
            : "  NOTE: ChoicesContainer hash not found directly in bundle (may be in decompressed data)");
    }

    private static void AddEventLogResults(List<string> results)
    {
        const ulong expected = EventLogFormat.ExecutingDialogNode;

        results.Add("  Format: EventLog records in estore/epage files");
        results.Add("  Record size: 42 bytes per entry");
        results.Add($"  Event type: ExecutingDialogNode (0x{expected:X16})");

        var computed = TelltaleCrc64.Compute("Executing Dialog Node");
        results.Add(computed == expected
            ? $"  MATCH: CRC64('executing dialog node') = 0x{computed:X16}"
            : $"  MISMATCH: CRC64('executing dialog node') = 0x{computed:X16}, expected 0x{expected:X16}");
    }

    private static void AddChoiceStatsResults(List<string> results)
    {
        results.Add("  Format: GUID StringValue in choicestats.pro PropertySet");
        results.Add("  String type hash: CRC64('string')");
        results.Add($"  CRC64('string') = 0x{TelltaleCrc64.Compute("String"):X16}");

        var bundlePath = Path.Combine(ToolPaths.TestData, Seasons.Season4, "wd4_saveslot1.bundle");
        if (!File.Exists(bundlePath))
        {
            return;
        }

        var guids = BracedGuid().Matches(TextFormat.DecodeLatin1(File.ReadAllBytes(bundlePath)));
        if (guids.Count == 0)
        {
            results.Add("  No GUIDs found directly in S4 bundle (may be compressed)");
            return;
        }

        results.Add($"  Found {guids.Count} GUIDs in S4 bundle");
        results.AddRange(guids.Take(5).Select(guid => $"    {guid.Value}"));
    }

    [GeneratedRegex(@"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}")]
    private static partial Regex BracedGuid();
}
