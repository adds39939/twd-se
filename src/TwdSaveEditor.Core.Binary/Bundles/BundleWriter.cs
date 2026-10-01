using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class BundleWriter
{
    public static byte[] Write(SaveSlot slot)
    {
        var psWriter = new PropertySetWriter();

        var innerFiles = new Dictionary<string, byte[]>();
        foreach (var entry in slot.FileTable)
        {
            if (entry.Name is "metadata_slot.p" or "metadata_save.p" && slot.Metadata != null)
            {
                var propBytes = psWriter.Write(slot.Metadata);
                innerFiles[entry.Name] = RebuildInnerMetaStream(slot.RawMetadataFile!, propBytes);
            }
            else if (entry.Name == slot.ChoicesFileName && slot.Choices != null)
            {
                var propBytes = psWriter.Write(slot.Choices);
                innerFiles[entry.Name] = RebuildInnerMetaStream(slot.RawChoicesFile!, propBytes);
            }
            else if (entry.Name == "choicestats.pro" && slot.ChoiceStats != null && slot.RawChoiceStatsFile != null)
            {
                var propBytes = psWriter.Write(slot.ChoiceStats);
                innerFiles[entry.Name] = RebuildInnerMetaStream(slot.RawChoiceStatsFile, propBytes);
            }
            else if (slot.RawInnerFiles != null && slot.RawInnerFiles.TryGetValue(entry.Name, out var raw))
            {
                innerFiles[entry.Name] = raw;
            }
            else
            {
                innerFiles[entry.Name] = [];
            }
        }

        return BuildBundle(slot.OuterHeader, slot.FileTable, innerFiles);
    }

    private static byte[] RebuildInnerMetaStream(byte[] originalInner, byte[] newPropData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        using var origMs = new MemoryStream(originalInner);
        using var origReader = new BinaryReaderEx(origMs);
        var magic = origReader.ReadUInt32();
        var origDefSize = origReader.ReadUInt32();
        var origDbgSize = origReader.ReadUInt32();
        var origAsyncSize = origReader.ReadUInt32();
        var verCount = origReader.ReadUInt32();

        var rawDefSize = (int)(origDefSize & 0x7FFFFFFF);
        var rawDbgSize = (int)(origDbgSize & 0x7FFFFFFF);
        var rawAsyncSize = (int)(origAsyncSize & 0x7FFFFFFF);

        var versionEntries = new List<(ulong, uint)>();
        for (uint i = 0; i < verCount; i++)
        {
            var tc = origReader.ReadUInt64();
            var vc = origReader.ReadUInt32();
            versionEntries.Add((tc, vc));
        }

        var headerDataEnd = (int)origMs.Position;
        var origDefData = origReader.ReadBytes(rawDefSize);
        var origDbgData = origReader.ReadBytes(rawDbgSize);
        var origAsyncData = origReader.ReadBytes(rawAsyncSize);

        writer.WriteUInt32(magic);
        writer.WriteUInt32((uint)newPropData.Length);
        writer.WriteUInt32(origDbgSize);
        writer.WriteUInt32(origAsyncSize);
        writer.WriteUInt32((uint)versionEntries.Count);
        foreach (var (tc, vc) in versionEntries)
        {
            writer.WriteUInt64(tc);
            writer.WriteUInt32(vc);
        }

        writer.WriteBytes(newPropData);

        if (rawDbgSize > 0)
            writer.WriteBytes(origDbgData);
        if (rawAsyncSize > 0)
            writer.WriteBytes(origAsyncData);

        writer.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildBundle(MetaStreamHeader outerHeader, List<BundleFileEntry> fileTable,
        Dictionary<string, byte[]> innerFiles)
    {
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

        byte[] fileTableData;
        using (var ftMs = new MemoryStream())
        using (var ftWriter = new BinaryWriterEx(ftMs, leaveOpen: true))
        {
            ftWriter.WriteUInt32(1);
            ftWriter.WriteUInt32((uint)newEntries.Count);

            foreach (var (name, offset, size, h1, h2) in newEntries)
            {
                ftWriter.WriteUInt32(offset);
                ftWriter.WriteUInt32(size);

                var nameBytes = Encoding.ASCII.GetBytes(name);
                ftWriter.WriteBytes(nameBytes);
                ftWriter.WriteByte(0);
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

        using var outMs = new MemoryStream();
        using var outWriter = new BinaryWriterEx(outMs, leaveOpen: true);

        outWriter.WriteUInt32(outerHeader.Magic);
        outWriter.WriteUInt32((uint)fileTableData.Length);
        outWriter.WriteUInt32(outerHeader.DebugDataSize);
        outWriter.WriteUInt32((uint)asyncData.Length);
        outWriter.WriteUInt32((uint)outerHeader.VersionEntries.Count);
        foreach (var ve in outerHeader.VersionEntries)
        {
            outWriter.WriteUInt64(ve.TypeCrc);
            outWriter.WriteUInt32(ve.VersionCrc);
        }
        outWriter.WriteBytes(fileTableData);

        var debugSize = (int)(outerHeader.DebugDataSize & 0x7FFFFFFF);
        if (debugSize > 0)
            outWriter.WriteBytes(new byte[debugSize]);

        outWriter.WriteBytes(asyncData);

        outWriter.Flush();
        return outMs.ToArray();
    }
}
