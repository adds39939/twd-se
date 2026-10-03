using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.ValidateSaves.Data;

namespace TwdSaveEditor.Tools.ValidateSaves.Validators;

public static class Season3ChoiceHashValidator
{
    private static readonly string[] EpageFiles =
    [
        "_wd3_saveslot1_id_Page734.epage",
        "_wd3_saveslot1_id_Page10249.epage",
        "_wd3_saveslot1_id_Page11215.epage",
        "_wd3_saveslot1_id_Page12180.epage",
    ];

    private static readonly Dictionary<ulong, (string Choice, string Option)> KnownHashes = new()
    {
        [0x2D4BB68B3A6B79B7] = ("shot_conrad", "true"),
        [0x95124598AFDC509B] = ("shot_conrad", "false"),
        [0xF650515EC9AA8356] = ("promised_kate", "false"),
        [0x05F6BEEEE2651B6C] = ("promised_kate", "true"),
        [0x80E6B5D0C042A4DB] = ("shot_joan", "true"),
        [0x30AC5DBC402A7EE4] = ("shot_joan", "false"),
        [0x65864B7A9C79F70F] = ("trippava_saved", "ava"),
        [0x37FD9A5893FC1E6D] = ("trippava_saved", "tripp"),
    };

    public static List<string> Validate()
    {
        var results = new List<string>();
        var foundCount = 0;
        var totalRecords = 0;

        foreach (var epageFile in EpageFiles)
        {
            var epagePath = Path.Combine(ToolPaths.TestData, Seasons.Season3, epageFile);
            if (!File.Exists(epagePath))
            {
                continue;
            }

            var data = File.ReadAllBytes(epagePath);
            var defaultField = Bytes.U32(data, 4);
            var defaultStart = 20 + Bytes.U32(data, 16) * 12L;

            if (MetaStreamParser.IsCompressed(defaultField))
            {
                results.Add($"  {epageFile}: compressed, skipping");
                continue;
            }

            var section = Bytes.Slice(data, defaultStart, defaultStart + MetaStreamParser.SectionSize(defaultField));
            if (Bytes.IndexOf(section, EventLogFormat.RecordHeader) < 0)
            {
                continue;
            }

            var records = EventLogFormat.ReadRecords(section);
            foreach (var record in records)
            {
                if (record.EventType != EventLogFormat.ExecutingDialogNode || !KnownHashes.TryGetValue(record.NodeHash, out var known))
                {
                    continue;
                }

                results.Add($"  MATCH: 0x{record.NodeHash:X16} -> {known.Choice}={known.Option} (in {epageFile})");
                foundCount++;
            }

            totalRecords += records.Count;
            results.Add($"  {epageFile}: {records.Count} records parsed");
        }

        results.Add($"\n  Summary: {foundCount} known hashes found in {totalRecords} total records");
        results.Add(foundCount > 0
            ? "  MATCH: ChoiceNodeMapping hashes confirmed in real game saves"
            : "  NOTE: No known hashes matched (save may not contain these specific choices)");

        return results;
    }
}
