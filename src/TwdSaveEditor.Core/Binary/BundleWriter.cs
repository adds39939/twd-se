using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Writes a modified TWD Definitive Series .bundle save file.
/// Replaces inner MetaStream files while preserving the bundle structure.
/// </summary>
public static class BundleWriter
{
    /// <summary>
    /// Write a bundle back to disk, replacing modified inner files.
    /// </summary>
    public static byte[] Write(SaveSlot slot)
    {
        var psWriter = new PropertySetWriter();

        // Serialize modified PropertySets back to inner MetaStream format
        var innerFiles = new Dictionary<string, byte[]>();
        foreach (var entry in slot.FileTable)
        {
            if (entry.Name == "metadata_slot.p" && slot.Metadata != null)
            {
                var propBytes = psWriter.Write(slot.Metadata);
                innerFiles[entry.Name] = RebuildInnerMetaStream(slot.RawMetadataFile!, propBytes);
            }
            else if (entry.Name == "choices.prop" && slot.Choices != null)
            {
                var propBytes = psWriter.Write(slot.Choices);
                innerFiles[entry.Name] = RebuildInnerMetaStream(slot.RawChoicesFile!, propBytes);
            }
            else if (slot.RawInnerFiles != null && slot.RawInnerFiles.TryGetValue(entry.Name, out var raw))
            {
                innerFiles[entry.Name] = raw;
            }
            else
            {
                // Fallback: empty placeholder (shouldn't happen with properly loaded saves)
                innerFiles[entry.Name] = [];
            }
        }

        return BuildBundle(slot.OuterHeader, slot.FileTable, innerFiles);
    }

    private static byte[] RebuildInnerMetaStream(byte[] originalInner, byte[] newPropData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        // Read original inner header to preserve version entries
        using var origMs = new MemoryStream(originalInner);
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

        // Write inner MetaStream header with new sizes (uncompressed)
        writer.WriteUInt32(magic);
        writer.WriteUInt32((uint)newPropData.Length); // new default section size (uncompressed)
        writer.WriteUInt32(0);                        // no debug section
        writer.WriteUInt32(0);                        // no async section
        writer.WriteUInt32((uint)versionEntries.Count);
        foreach (var (tc, vc) in versionEntries)
        {
            writer.WriteUInt64(tc);
            writer.WriteUInt32(vc);
        }

        // Write new default section (PropertySet data)
        writer.WriteBytes(newPropData);

        writer.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildBundle(MetaStreamHeader outerHeader, List<BundleFileEntry> fileTable,
        Dictionary<string, byte[]> innerFiles)
    {
        // Build async section: concatenate all inner files
        using var asyncMs = new MemoryStream();
        var newEntries = new List<(string name, uint offset, uint size, ulong h1, ulong h2)>();
        uint asyncOffset = 0;

        foreach (var entry in fileTable)
        {
            var data = innerFiles[entry.Name];
            newEntries.Add((entry.Name, asyncOffset, (uint)data.Length, entry.Hash1, entry.Hash2));
            asyncMs.Write(data, 0, data.Length);
            asyncOffset += (uint)data.Length;
        }

        var asyncData = asyncMs.ToArray();

        // Build file table (default section)
        byte[] fileTableData;
        using (var ftMs = new MemoryStream())
        using (var ftWriter = new BinaryWriterEx(ftMs, leaveOpen: true))
        {
            ftWriter.WriteUInt32(1); // unknown, always 1
            ftWriter.WriteUInt32((uint)newEntries.Count);

            foreach (var (name, offset, size, h1, h2) in newEntries)
            {
                ftWriter.WriteUInt32(offset);
                ftWriter.WriteUInt32(size);

                var nameBytes = Encoding.ASCII.GetBytes(name);
                ftWriter.WriteBytes(nameBytes);
                ftWriter.WriteByte(0); // null terminator
                var nameLen = nameBytes.Length + 1;
                var padded = (nameLen + 3) & ~3;
                for (int i = 0; i < padded - nameLen; i++)
                    ftWriter.WriteByte(0);

                ftWriter.WriteUInt64(h1);
                ftWriter.WriteUInt64(h2);
            }

            ftWriter.Flush();
            fileTableData = ftMs.ToArray();
        }

        // Build outer MetaStream (always write uncompressed async for simplicity)
        using var outMs = new MemoryStream();
        using var outWriter = new BinaryWriterEx(outMs, leaveOpen: true);

        outWriter.WriteUInt32(outerHeader.Magic);
        outWriter.WriteUInt32((uint)fileTableData.Length);
        outWriter.WriteUInt32(outerHeader.DebugDataSize); // preserve original debug size
        outWriter.WriteUInt32((uint)asyncData.Length);     // uncompressed async
        outWriter.WriteUInt32((uint)outerHeader.VersionEntries.Count);
        foreach (var ve in outerHeader.VersionEntries)
        {
            outWriter.WriteUInt64(ve.TypeCrc);
            outWriter.WriteUInt32(ve.VersionCrc);
        }
        outWriter.WriteBytes(fileTableData);

        // Write debug section (zeros to match original size)
        var debugSize = (int)(outerHeader.DebugDataSize & 0x7FFFFFFF);
        if (debugSize > 0)
            outWriter.WriteBytes(new byte[debugSize]);

        outWriter.WriteBytes(asyncData);

        outWriter.Flush();
        return outMs.ToArray();
    }
}
