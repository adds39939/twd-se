using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Collections;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.DecodeEstore.Model;

namespace TwdSaveEditor.Tools.DecodeEstore.Analysis;

public static class EpageAnalyzer
{
    private const int HeaderSize = 12;

    public static EpageAnalysis? Analyze(string path, TextWriter output)
    {
        var name = Path.GetFileName(path);
        var sections = MetaStreamParser.Parse(File.ReadAllBytes(path));
        if (sections == null)
        {
            output.WriteLine($"  FAILED to parse MSV6 for {name}");
            return null;
        }

        var defaultSection = sections.Default;
        if (defaultSection.Length == 0)
        {
            output.WriteLine($"  No default section in {name}");
            return null;
        }

        var nameStart = Bytes.IndexOf(defaultSection, ".epage"u8);
        var region = defaultSection.AsSpan(Math.Min(nameStart >= 0 ? nameStart + 6 : HeaderSize, defaultSection.Length));

        var occurrences = Bytes.FindAll(region, EventLogFormat.RecordStart);
        if (occurrences.Count == 0)
        {
            output.WriteLine($"  No record pattern found in {name}");
            return null;
        }

        if (occurrences.Count < 2)
            return null;

        var recordSize = DominantSpacing(occurrences);
        var recordsStart = occurrences[0] - BestAlignment(region, occurrences[0], recordSize);
        var recordCount = (region.Length - recordsStart) / recordSize;
        var hashOffset = FindHashOffset(region, recordsStart, recordSize, recordCount);

        var typeCounts = new Counter<string>();
        var typeHashes = new SortedSet<ulong>();
        var records = new List<EventRecord>();

        for (var i = 0; i < recordCount; i++)
        {
            var position = recordsStart + i * recordSize;
            if (position + recordSize > region.Length)
                break;

            var raw = region.Slice(position, recordSize).ToArray();
            string? eventType = null;
            if (hashOffset >= 0 && hashOffset + 8 <= recordSize)
            {
                var hash = Bytes.U64(raw, hashOffset);
                typeHashes.Add(hash);
                eventType = EventTypeNames.Describe(hash, $"unknown(0x{hash:X16})");
                typeCounts.Add(eventType);
            }

            records.Add(new EventRecord(i, raw, eventType));
        }

        return new EpageAnalysis(name, defaultSection, recordSize, hashOffset, records, typeCounts, typeHashes);
    }

    private static int DominantSpacing(List<int> occurrences)
    {
        var spacings = new Counter<int>();
        for (var i = 1; i < occurrences.Count; i++)
            spacings.Add(occurrences[i] - occurrences[i - 1]);

        return spacings.MostCommon(1)[0].Key;
    }

    private static int BestAlignment(ReadOnlySpan<byte> region, int firstOccurrence, int recordSize)
    {
        var bestOffset = 0;
        var bestMatches = 0;
        for (var trialOffset = 0; trialOffset < recordSize; trialOffset++)
        {
            var trialStart = firstOccurrence - trialOffset;
            if (trialStart < 0)
                continue;

            var matches = 0;
            for (var i = 0; i < Math.Min(50, region.Length / recordSize); i++)
            {
                var position = trialStart + i * recordSize;
                if (position + recordSize > region.Length)
                    break;

                for (var offset = 0; offset < Math.Max(0, recordSize - 8); offset++)
                {
                    if (!EventTypeNames.IsKnown(Bytes.U64(region, position + offset)))
                        continue;

                    matches++;
                    break;
                }
            }

            if (matches > bestMatches)
            {
                bestMatches = matches;
                bestOffset = trialOffset;
            }
        }

        return bestOffset;
    }

    private static int FindHashOffset(ReadOnlySpan<byte> region, int recordsStart, int recordSize, int recordCount)
    {
        for (var offset = 0; offset < recordSize - 7; offset++)
        {
            var matches = 0;
            for (var i = 0; i < Math.Min(10, recordCount); i++)
            {
                var position = recordsStart + i * recordSize;
                if (position + offset + 8 > region.Length)
                    break;
                if (EventTypeNames.IsKnown(Bytes.U64(region, position + offset)))
                    matches++;
            }

            if (matches >= Math.Min(5, recordCount))
                return offset;
        }

        return -1;
    }
}
