using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Collections;
using TwdSaveEditor.Tools.DecodeEstore.Model;

namespace TwdSaveEditor.Tools.DecodeEstore.Reporting;

public sealed class PageComparison(TextWriter output)
{
    private const int ListedDifferences = 20;

    public void Report(EpageAnalysis first, EpageAnalysis second, string sizeSuffix, bool analyseValues)
    {
        if (first.Default.Length != second.Default.Length)
        {
            output.WriteLine($"  Different sizes: {first.Default.Length} vs {second.Default.Length}");
            return;
        }

        var differences = new List<ByteDifference>();
        for (var i = 0; i < first.Default.Length; i++)
        {
            if (first.Default[i] != second.Default[i])
                differences.Add(new ByteDifference(i, first.Default[i], second.Default[i]));
        }

        output.WriteLine($"  Size: {first.Default.Length} bytes{sizeSuffix}");
        output.WriteLine($"  Differing bytes: {differences.Count}");
        if (differences.Count == 0)
            return;

        var nameEnd = Bytes.IndexOf(first.Default, ".epage"u8) + 6;
        ReportPositions(differences, nameEnd, first.RecordSize);

        if (analyseValues)
            ReportValues(differences);
    }

    private void ReportPositions(List<ByteDifference> differences, int nameEnd, int recordSize)
    {
        var positions = new Counter<int>();
        foreach (var difference in differences)
        {
            if (difference.Offset - nameEnd >= 0 && recordSize > 0)
                positions.Add((difference.Offset - nameEnd) % recordSize);
        }

        output.WriteLine();
        output.WriteLine($"  Differences by byte position within {recordSize}-byte records:");
        foreach (var (position, count) in positions.MostCommon())
            output.WriteLine($"    byte[{position}]: {count} differences");

        output.WriteLine();
        output.WriteLine($"  First {ListedDifferences} differences:");
        foreach (var difference in differences.Take(ListedDifferences))
        {
            var adjusted = difference.Offset - nameEnd;
            var recordIndex = recordSize > 0 ? (int)Math.Floor((double)adjusted / recordSize) : -1;
            var bytePosition = recordSize > 0 ? adjusted - recordIndex * recordSize : -1;
            output.WriteLine($"    offset 0x{difference.Offset:X4} (record {recordIndex}, byte {bytePosition}): Ep1=0x{difference.First:X2} Ep5=0x{difference.Second:X2}");
        }
    }

    private void ReportValues(List<ByteDifference> differences)
    {
        var firstValues = new Counter<byte>();
        var secondValues = new Counter<byte>();
        foreach (var difference in differences)
        {
            firstValues.Add(difference.First);
            secondValues.Add(difference.Second);
        }

        output.WriteLine();
        output.WriteLine("  Analysis of changing byte (byte[3] = likely 'visited/state' flag):");
        output.WriteLine($"    Ep1 values: {Describe(firstValues)}");
        output.WriteLine($"    Ep5 values: {Describe(secondValues)}");
    }

    private static string Describe(Counter<byte> values) =>
        "{" + string.Join(", ", values.MostCommon(10).Select(pair => $"{pair.Key}: {pair.Value}")) + "}";
}
