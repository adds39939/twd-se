using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.ValidateSaves.Data;

namespace TwdSaveEditor.Tools.ValidateSaves.Validators;

public static class EstoreFormatValidator
{
    public static List<string> Validate(string season)
    {
        var results = new List<string>();

        var files = season switch
        {
            Seasons.Season3 => ("_wd3_saveslot1_id.estore", "_wd3_saveslot1_id_Page734.epage"),
            Seasons.Michonne => ("_wdm_saveslot4_id.estore", "_wdm_saveslot4_id_Page971.epage"),
            _ => ((string Estore, string Epage)?)null,
        };

        if (files is not var (estoreFile, epageFile))
        {
            results.Add($"  SKIP: {season} doesn't use estore/epage");
            return results;
        }

        var estorePath = Path.Combine(ToolPaths.TestData, season, estoreFile);
        if (!File.Exists(estorePath))
        {
            results.Add($"  SKIP: {estorePath} not found");
            return results;
        }

        AddEstoreResults(results, File.ReadAllBytes(estorePath));

        var epagePath = Path.Combine(ToolPaths.TestData, season, epageFile);
        if (File.Exists(epagePath))
        {
            AddEpageResults(results, File.ReadAllBytes(epagePath));
        }

        return results;
    }

    private static void AddEstoreResults(List<string> results, byte[] estore)
    {
        results.Add($"  Real estore: {estore.Length} bytes");

        var magic = Bytes.U32(estore, 0);
        results.Add($"  estore magic: {MetaStreamParser.MagicName(magic)}");
        results.Add(magic == MetaStreamParser.MagicMsv6
            ? "  MATCH: estore uses MSV6 (matches EStoreCreator)"
            : $"  MISMATCH: estore uses {MetaStreamParser.MagicName(magic)}, expected MSV6");

        var versionCount = Bytes.U32(estore, 16);
        results.Add($"  estore version entries: {versionCount}");

        long position = 20;
        var versions = MetaStreamParser.ReadVersionEntries(estore, ref position, versionCount);
        var expectedEntries = SaveFormat.InnerVersionEntries;

        if (versions.Count != expectedEntries.Length)
        {
            results.Add($"  NOTE: {versions.Count} version entries vs {expectedEntries.Length} expected");
        }
        else if (versions.SequenceEqual(expectedEntries))
        {
            results.Add("  MATCH: estore version entries match EStoreCreator.VersionEntries");
        }
        else
        {
            foreach (var (real, expected) in versions.Zip(expectedEntries))
            {
                if (real != expected)
                {
                    results.Add($"  MISMATCH: ver 0x{real.Type:X16}/0x{real.Version:X8} != 0x{expected.Type:X16}/0x{expected.Version:X8}");
                }
            }
        }
    }

    private static void AddEpageResults(List<string> results, byte[] epage)
    {
        results.Add($"\n  Real epage: {epage.Length} bytes");
        results.Add($"  epage magic: {MetaStreamParser.MagicName(Bytes.U32(epage, 0))}");

        var defaultField = Bytes.U32(epage, 4);
        var defaultSize = MetaStreamParser.SectionSize(defaultField);
        var defaultStart = 20 + Bytes.U32(epage, 16) * 12L;

        if (MetaStreamParser.IsCompressed(defaultField))
        {
            results.Add($"  epage default section is compressed ({defaultSize} bytes)");
            return;
        }

        var section = Bytes.Slice(epage, defaultStart, defaultStart + defaultSize);
        var recordStart = Bytes.IndexOf(section, EventLogFormat.RecordHeader);
        if (recordStart < 0)
        {
            results.Add("  WARNING: No 42-byte record pattern found in epage");
            return;
        }

        var recordCount = (section.Length - recordStart) / EventLogFormat.RecordSize;
        results.Add($"  Records start at offset {recordStart} in default section");
        results.Add($"  Record count: {recordCount} (42 bytes each)");
        results.Add("  MATCH: 42-byte record format confirmed");

        if (recordCount == 0)
        {
            return;
        }

        var record = section.Slice(recordStart, EventLogFormat.RecordSize);
        var eventHash = Bytes.U64(record, EventLogFormat.EventTypeOffset);
        results.Add("  First record:");
        results.Add($"    version=0x{Bytes.U32(record, 0):X}, payload=0x{Bytes.U32(record, 4):X}, count={Bytes.U32(record, 8)}, padding={Bytes.U32(record, 12)}");
        results.Add($"    eventTypeHash=0x{eventHash:X16}");
        results.Add($"    valueType={Bytes.U32(record, 24)}, extraFlag={record[28]}");
        results.Add($"    nodeHash=0x{Bytes.U64(record, EventLogFormat.NodeHashOffset):X16}");
        results.Add($"    sequenceIndex={record[37] | (record[38] << 8) | (record[39] << 16)}");
        results.Add($"    trailing=0x{Bytes.U16(record, 40):X4}");
        results.Add($"    Event type: {SaveFormat.EventTypeName(eventHash)}");
    }
}
