using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Collections;
using TwdSaveEditor.Tools.DecodeEstore.Analysis;
using TwdSaveEditor.Tools.DecodeEstore.Model;

namespace TwdSaveEditor.Tools.DecodeEstore.Reporting;

public sealed class DecodeReport(string savesRoot, TextWriter output)
{
    private const string Season3Estore = "_wd3_saveslot1_id.estore";
    private const string Season3Pages = "_wd3_saveslot1_id_Page*.epage";
    private const string Page734 = "_wd3_saveslot1_id_Page734.epage";
    private const string Page10249 = "_wd3_saveslot1_id_Page10249.epage";

    private static readonly int[] MichonneSlots = [3, 2, 4];

    private readonly OrderedDictionary<string, EpageAnalysis> _episode1Pages = [];
    private readonly OrderedDictionary<string, EpageAnalysis> _episode5Pages = [];

    public void Run()
    {
        output.WriteLine("=== Known Event Type CRC64 Hashes ===");
        foreach (var (hash, name) in EventTypeNames.ByHash.OrderBy(pair => pair.Value, StringComparer.Ordinal))
        {
            output.WriteLine($"  0x{hash:X16} = {name}");
        }

        Banner("TELLTALE EVENTLOG ESTORE/EPAGE FORMAT DECODER");

        ReportEpisode1();
        ReportEpisode5();
        Compare(Page734, "COMPARISON: Page734 - Ep1 vs Ep5", " (identical)", analyseValues: true);
        Compare(Page10249, "COMPARISON: Page10249 - Ep1 vs Ep5", "", analyseValues: false);
        ReportMichonne();
        ReportSummary();

        Banner("FINAL RECORD FORMAT (CONFIRMED)");
        output.WriteLine(RecordFormatDescription.Text);
    }

    private void ReportEpisode1()
    {
        var directory = Path.Combine(savesRoot, "S3", "Episode 1");
        Banner("S3 EPISODE 1");

        if (EstoreAnalyzer.Analyze(Path.Combine(directory, Season3Estore)) is { } estore)
        {
            output.WriteLine();
            output.WriteLine($"Estore: {estore.FileSize} bytes, default section: {estore.DefaultSize} bytes");
            output.WriteLine($"  Header hash: 0x{estore.HeaderHash:X16}");
            PrintPages(estore);
        }

        foreach (var page in AnalyzePages(directory, Season3Pages))
        {
            _episode1Pages[page.Name] = page;
            output.WriteLine();
            output.WriteLine($"  {page.Name}:");
            output.WriteLine($"    Default section: {page.Default.Length} bytes");
            output.WriteLine($"    Record size: {page.RecordSize} bytes");
            output.WriteLine($"    Record count: {page.Records.Count}");
            output.WriteLine($"    Event type hash at offset: {page.HashOffset}");
            PrintEventTypes(page);
            PrintTypeHashes(page);
            PrintFirstRecords(page, "First 5 records (hex):");
        }
    }

    private void ReportEpisode5()
    {
        var directory = Path.Combine(savesRoot, "S3", "Episode 5", "The end");
        Banner("S3 EPISODE 5 (The End)");

        if (EstoreAnalyzer.Analyze(Path.Combine(directory, Season3Estore)) is { } estore)
        {
            output.WriteLine();
            output.WriteLine($"Estore: {estore.FileSize} bytes, default section: {estore.DefaultSize} bytes");
            PrintPages(estore);
        }

        foreach (var page in AnalyzePages(directory, Season3Pages))
        {
            _episode5Pages[page.Name] = page;
            output.WriteLine();
            output.WriteLine($"  {page.Name}:");
            output.WriteLine($"    Record size: {page.RecordSize}, count: {page.Records.Count}");
            PrintEventTypes(page);
        }
    }

    private void Compare(string name, string title, string sizeSuffix, bool analyseValues)
    {
        if (!_episode1Pages.TryGetValue(name, out var first) || !_episode5Pages.TryGetValue(name, out var second))
        {
            return;
        }

        Banner(title);
        new PageComparison(output).Report(first, second, sizeSuffix, analyseValues);
    }

    private void ReportMichonne()
    {
        var directory = Path.Combine(savesRoot, "Michonne");
        Banner("MICHONNE");

        foreach (var slot in MichonneSlots)
        {
            var estorePath = Path.Combine(directory, $"_wdm_saveslot{slot}_id.estore");
            if (!File.Exists(estorePath))
            {
                continue;
            }

            if (EstoreAnalyzer.Analyze(estorePath) is { } estore)
            {
                output.WriteLine();
                output.WriteLine($"Estore (slot {slot}): {estore.FileSize} bytes, default: {estore.DefaultSize} bytes");
                PrintPages(estore);
            }

            foreach (var page in AnalyzePages(directory, $"_wdm_saveslot{slot}_id_Page*.epage"))
            {
                output.WriteLine();
                output.WriteLine($"  {page.Name}:");
                output.WriteLine($"    Record size: {page.RecordSize}, count: {page.Records.Count}");
                output.WriteLine($"    Event type hash offset: {page.HashOffset}");
                PrintEventTypes(page);
                PrintTypeHashes(page);
                PrintFirstRecords(page, "First 5 records:");
            }

            break;
        }
    }

    private void ReportSummary()
    {
        Banner("STRUCTURAL ANALYSIS SUMMARY");

        var recordSizes = new SortedSet<int>();
        var hashOffsets = new SortedSet<int>();
        var totalRecords = 0;
        var totalEventTypes = new Counter<string>();

        foreach (var page in _episode1Pages.Values.Concat(_episode5Pages.Values))
        {
            recordSizes.Add(page.RecordSize);
            hashOffsets.Add(page.HashOffset);
            totalRecords += page.Records.Count;
            totalEventTypes.AddRange(page.TypeCounts);
        }

        output.WriteLine();
        output.WriteLine($"  Record sizes observed: [{string.Join(", ", recordSizes)}]");
        output.WriteLine($"  Event type hash offsets within records: [{string.Join(", ", hashOffsets)}]");
        output.WriteLine($"  Total records analyzed: {totalRecords}");
        output.WriteLine();
        output.WriteLine("  Overall event type distribution:");
        foreach (var (eventType, count) in totalEventTypes.MostCommon())
        {
            var percentage = totalRecords > 0 ? (double)count / totalRecords * 100 : 0;
            output.WriteLine($"    {eventType}: {count} ({percentage:F1}%)");
        }

        if (_episode1Pages.TryGetValue(Page734, out var reference))
        {
            ReportRecordStructure(reference);
        }
    }

    private void ReportRecordStructure(EpageAnalysis page)
    {
        var records = page.Records;
        var recordSize = page.RecordSize;

        output.WriteLine();
        output.WriteLine($"  Detailed record structure analysis (Page734, {recordSize}-byte records):");
        output.WriteLine($"  Event type hash at byte offset {page.HashOffset}");

        if (records.Count >= 10)
        {
            for (var position = 0; position < recordSize; position++)
            {
                var values = new SortedSet<byte>(records.Take(200).Where(record => position < record.Raw.Length).Select(record => record.Raw[position]));
                if (values.Count == 1)
                {
                    output.WriteLine($"    byte[{position,2}]: CONSTANT 0x{values.Min:X2} ({values.Min})");
                }
                else if (values.Count <= 5)
                {
                    output.WriteLine($"    byte[{position,2}]: {values.Count} values: {string.Join(", ", values.Select(value => $"0x{value:X2}"))}");
                }
                else
                {
                    output.WriteLine($"    byte[{position,2}]: VARIABLE ({values.Count} distinct values)");
                }
            }
        }

        output.WriteLine();
        output.WriteLine("  Sequential index search:");
        PrintSequentialFields(records, recordSize - 3, "uint32", (raw, offset) => Bytes.U32(raw, offset));
        PrintSequentialFields(records, recordSize - 1, "uint16", (raw, offset) => Bytes.U16(raw, offset));
    }

    private IEnumerable<EpageAnalysis> AnalyzePages(string directory, string pattern)
    {
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, pattern).Order(StringComparer.Ordinal).ToList()
            : [];
        output.WriteLine();
        output.WriteLine($"  Found {files.Count} epage files");

        foreach (var file in files)
        {
            if (EpageAnalyzer.Analyze(file, output) is { } page)
            {
                yield return page;
            }
        }
    }

    private void Banner(string title)
    {
        output.WriteLine();
        output.WriteLine(new string('=', 80));
        output.WriteLine(title);
        output.WriteLine(new string('=', 80));
    }

    private void PrintPages(EstoreAnalysis estore)
    {
        output.WriteLine($"  Page count: {estore.Pages.Count}");
        foreach (var page in estore.Pages)
        {
            output.WriteLine($"    Page {page.Number}: hash=0x{page.Hash:X16}");
        }

        output.WriteLine($"  Event records in estore: {estore.RecordCount}");
    }

    private void PrintEventTypes(EpageAnalysis page)
    {
        output.WriteLine("    Event types:");
        foreach (var (eventType, count) in page.TypeCounts.MostCommon())
        {
            output.WriteLine($"      {eventType}: {count}");
        }
    }

    private void PrintTypeHashes(EpageAnalysis page)
    {
        output.WriteLine($"    Unique type hashes: {page.TypeHashes.Count}");
        foreach (var hash in page.TypeHashes)
        {
            output.WriteLine($"      0x{hash:X16} = {EventTypeNames.Describe(hash, "UNKNOWN")}");
        }
    }

    private void PrintFirstRecords(EpageAnalysis page, string title)
    {
        output.WriteLine($"    {title}");
        foreach (var record in page.Records.Take(5))
        {
            output.WriteLine($"      [{record.Index,4}] {string.Join(' ', record.Raw.Select(value => value.ToString("X2")))}");
            output.WriteLine($"             type={record.EventType ?? "None"}");
        }
    }

    private void PrintSequentialFields(List<EventRecord> records, int fieldCount, string label, Func<byte[], int, uint> read)
    {
        for (var start = 0; start < fieldCount; start++)
        {
            var values = records.Take(30).Select(record => read(record.Raw, start)).ToList();
            if (values.Count < 5)
            {
                continue;
            }

            var sequential = values.Zip(values.Skip(1)).All(pair => pair.Second > pair.First);
            if (sequential && (long)values[^1] - values[0] < values.Count * 3)
            {
                output.WriteLine($"    {label} at byte[{start}]: sequential, range {values[0]}-{values[^1]}");
            }
        }
    }
}
