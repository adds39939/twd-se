using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Writes modified EventLog entries back to epage files.
/// Modifies records in-place within existing epage files to preserve the overall structure.
/// </summary>
public static class EStoreWriter
{
    /// <summary>
    /// Write a modified EventLog entry back to the epage file that contains it.
    /// Finds the record by its original position and overwrites the 42-byte record in-place.
    /// </summary>
    public static void WriteEntryToPage(string epagePath, int recordIndex, EventLogEntry entry)
    {
        var data = File.ReadAllBytes(epagePath);
        var sections = EStoreReader.ReadMetaStreamSections(data);

        // Find record start in default section
        var defaultData = sections.defaultData;
        int startOffset = FindRecordStart(defaultData);
        if (startOffset < 0) return;

        int offset = startOffset + recordIndex * EventLogEntry.RecordSize;
        if (offset + EventLogEntry.RecordSize > defaultData.Length) return;

        // Build the new 42-byte record
        var record = BuildRecord(entry);
        Array.Copy(record, 0, defaultData, offset, EventLogEntry.RecordSize);

        // Rebuild the epage file with the modified default section
        var rebuilt = RebuildMetaStream(data, defaultData);
        File.WriteAllBytes(epagePath, rebuilt);
    }

    /// <summary>
    /// Build a 42-byte EventLog record from an entry.
    /// </summary>
    public static byte[] BuildRecord(EventLogEntry entry)
    {
        var record = new byte[EventLogEntry.RecordSize];

        // Header: version(4) + payload_size(4) + count(4) + padding(4)
        BitConverter.GetBytes((uint)0x0A).CopyTo(record, 0);
        BitConverter.GetBytes((uint)0x22).CopyTo(record, 4);
        BitConverter.GetBytes((uint)0x01).CopyTo(record, 8);
        // padding at 12 = 0

        // Event type hash
        BitConverter.GetBytes(entry.EventTypeHash).CopyTo(record, 16);

        // Value type
        BitConverter.GetBytes(entry.ValueType).CopyTo(record, 24);

        // Extra flag
        record[28] = entry.ExtraFlag;

        // Node hash
        BitConverter.GetBytes(entry.NodeHash).CopyTo(record, 29);

        // Sequence index (3 bytes LE)
        record[37] = (byte)(entry.SequenceIndex & 0xFF);
        record[38] = (byte)((entry.SequenceIndex >> 8) & 0xFF);
        record[39] = (byte)((entry.SequenceIndex >> 16) & 0xFF);

        // Trailing
        BitConverter.GetBytes(entry.Trailing).CopyTo(record, 40);

        return record;
    }

    /// <summary>
    /// Rebuild a MetaStream file with a modified default section (uncompressed).
    /// Preserves the original header structure and version entries.
    /// </summary>
    private static byte[] RebuildMetaStream(byte[] original, byte[] newDefaultData)
    {
        using var origMs = new MemoryStream(original);
        using var origReader = new BinaryReaderEx(origMs);

        var magic = origReader.ReadUInt32();
        var origDefSize = origReader.ReadUInt32();
        var origDbgSize = origReader.ReadUInt32();
        var origAsyncSize = origReader.ReadUInt32();
        var verCount = origReader.ReadUInt32();

        var versionEntries = new List<(ulong, uint)>();
        for (uint i = 0; i < verCount; i++)
        {
            var tc = origReader.ReadUInt64();
            var vc = origReader.ReadUInt32();
            versionEntries.Add((tc, vc));
        }

        // Read debug and async sections from original
        var origDefRawSize = (int)(origDefSize & 0x7FFFFFFF);
        origReader.ReadBytes(origDefRawSize); // skip original default section

        var dbgRawSize = (int)(origDbgSize & 0x7FFFFFFF);
        var dbgData = dbgRawSize > 0 ? origReader.ReadBytes(dbgRawSize) : [];

        var asyncRawSize = (int)(origAsyncSize & 0x7FFFFFFF);
        var asyncData = asyncRawSize > 0 ? origReader.ReadBytes(asyncRawSize) : [];

        // Write rebuilt MetaStream
        using var outMs = new MemoryStream();
        using var writer = new BinaryWriterEx(outMs, leaveOpen: true);

        writer.WriteUInt32(magic);
        writer.WriteUInt32((uint)newDefaultData.Length); // uncompressed
        writer.WriteUInt32(origDbgSize); // preserve debug compression flag
        writer.WriteUInt32(origAsyncSize); // preserve async
        writer.WriteUInt32((uint)versionEntries.Count);
        foreach (var (tc, vc) in versionEntries)
        {
            writer.WriteUInt64(tc);
            writer.WriteUInt32(vc);
        }

        writer.WriteBytes(newDefaultData);
        if (dbgData.Length > 0) writer.WriteBytes(dbgData);
        if (asyncData.Length > 0) writer.WriteBytes(asyncData);

        writer.Flush();
        return outMs.ToArray();
    }

    private static int FindRecordStart(byte[] data)
    {
        byte[] pattern = [0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
