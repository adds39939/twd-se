using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Collections;
using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.EventLog;

public static class EpageHashReader
{
    private const int HeaderSize = 12;
    private const int NodeHashDistance = EventLogFormat.NodeHashOffset - EventLogFormat.EventTypeOffset;

    private static readonly int[] EventTypeOffsets = [16, 24];

    public static EpageHashes? Read(string path, TextWriter output)
    {
        var defaultSection = MetaStreamParser.Parse(File.ReadAllBytes(path))?.Default;
        if (defaultSection is not { Length: > 0 })
            return null;

        var nameEnd = Bytes.IndexOf(defaultSection, ".epage"u8);
        var region = defaultSection.AsSpan(nameEnd >= 0 ? nameEnd + 6 : HeaderSize);

        var occurrences = Bytes.FindAll(region, EventLogFormat.RecordStart);
        if (occurrences.Count < 2)
            return null;

        var spacings = new Counter<int>();
        for (var i = 1; i < Math.Min(100, occurrences.Count); i++)
            spacings.Add(occurrences[i] - occurrences[i - 1]);

        var recordSize = spacings.MostCommon(1)[0].Key;
        if (recordSize != EventLogFormat.RecordSize)
            output.WriteLine($"  WARNING: Record size {recordSize} != {EventLogFormat.RecordSize} in {path}");

        var alignment = BestAlignment(region, occurrences[0], recordSize);
        if (alignment < 0)
            return null;

        var recordStart = occurrences[0] - alignment;
        var recordCount = (region.Length - recordStart) / recordSize;

        var eventTypeOffset = FindEventTypeOffset(region, recordStart, recordSize, recordCount);
        if (eventTypeOffset < 0)
            return null;

        var nodeHashOffset = eventTypeOffset + NodeHashDistance;
        var hashes = new EpageHashes();

        for (var i = 0; i < recordCount; i++)
        {
            var record = recordStart + i * recordSize;
            if (record + nodeHashOffset + 8 > region.Length)
                break;

            var eventType = Bytes.U64(region, record + eventTypeOffset);
            if (eventType == EventLogFormat.ExecutingDialogNode)
                hashes.Nodes.Add(Bytes.U64(region, record + nodeHashOffset));
            else if (eventType == EventLogFormat.DialogChoice)
                hashes.Choices.Add(Bytes.U64(region, record + nodeHashOffset));
        }

        return hashes;
    }

    private static bool IsDialogEvent(ulong hash) => hash is EventLogFormat.ExecutingDialogNode or EventLogFormat.DialogChoice;

    private static int BestAlignment(ReadOnlySpan<byte> region, int firstOccurrence, int recordSize)
    {
        var bestAlignment = -1;
        var bestMatchCount = 0;
        for (var alignment = 0; alignment < recordSize; alignment++)
        {
            var trialStart = firstOccurrence - alignment;
            if (trialStart < 0)
                continue;

            var matches = 0;
            for (var i = 0; i < Math.Min(50, (region.Length - trialStart) / recordSize); i++)
            {
                var record = trialStart + i * recordSize;
                if (record + recordSize > region.Length)
                    break;

                foreach (var hashOffset in EventTypeOffsets)
                {
                    if (record + hashOffset + 8 > region.Length || !IsDialogEvent(Bytes.U64(region, record + hashOffset)))
                        continue;

                    matches++;
                    break;
                }
            }

            if (matches > bestMatchCount)
            {
                bestMatchCount = matches;
                bestAlignment = alignment;
            }
        }

        return bestAlignment;
    }

    private static int FindEventTypeOffset(ReadOnlySpan<byte> region, int recordStart, int recordSize, int recordCount)
    {
        for (var offset = 0; offset < recordSize - 7; offset++)
        {
            var matches = 0;
            for (var i = 0; i < Math.Min(50, recordCount); i++)
            {
                var record = recordStart + i * recordSize;
                if (record + offset + 8 > region.Length)
                    break;
                if (IsDialogEvent(Bytes.U64(region, record + offset)))
                    matches++;
            }

            if (matches >= Math.Min(5, recordCount / 2))
                return offset;
        }

        return -1;
    }
}
