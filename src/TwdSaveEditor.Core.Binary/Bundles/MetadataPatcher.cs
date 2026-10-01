using System.IO.Compression;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class MetadataPatcher
{
    public static byte[] PatchMetadata(byte[] originalBundle, PropertySet metadata, byte[] rawMetadataFile,
        bool clearDefaultSave = false)
    {
        var psWriter = new PropertySetWriter();
        var newPropBytes = psWriter.Write(metadata);
        var newInnerFile = RebuildInnerMetaStream(rawMetadataFile, newPropBytes);

        var defSizeField = BitConverter.ToUInt32(originalBundle, 4);
        var dbgSizeField = BitConverter.ToUInt32(originalBundle, 8);
        var asyncSizeField = BitConverter.ToUInt32(originalBundle, 12);
        var verCount = BitConverter.ToUInt32(originalBundle, 16);
        var headerEnd = 20 + (int)verCount * 12;
        var defSize = (int)(defSizeField & 0x7FFFFFFF);
        var dbgSize = (int)(dbgSizeField & 0x7FFFFFFF);
        var asyncStart = headerEnd + defSize + dbgSize;
        var asyncRawSize = (int)(asyncSizeField & 0x7FFFFFFF);
        var asyncCompressed = (asyncSizeField & 0x80000000) != 0;

        var (metaOffset, metaSize) = FindMetadataInFileTable(originalBundle, headerEnd, defSize);
        if (metaOffset < 0)
            throw new InvalidOperationException("Could not find metadata file in bundle.");

        byte[] asyncData;
        if (asyncCompressed)
        {
            var rawAsync = new byte[asyncRawSize];
            Array.Copy(originalBundle, asyncStart, rawAsync, 0, asyncRawSize);
            asyncData = DecompressSection(rawAsync);
        }
        else
        {
            asyncData = new byte[asyncRawSize];
            Array.Copy(originalBundle, asyncStart, asyncData, 0, asyncRawSize);
        }

        if (clearDefaultSave)
        {
            var (defSaveOffset, defSaveSize) = FindFileInFileTable(
                originalBundle, headerEnd, defSize, "default.save");
            if (defSaveOffset >= 0 && defSaveOffset + defSaveSize <= asyncData.Length)
            {
                Array.Clear(asyncData, defSaveOffset, defSaveSize);
            }
        }

        var sizeDiff = newInnerFile.Length - metaSize;
        var newAsyncData = new byte[asyncData.Length + sizeDiff];

        Array.Copy(asyncData, 0, newAsyncData, 0, metaOffset);
        Array.Copy(newInnerFile, 0, newAsyncData, metaOffset, newInnerFile.Length);
        var afterOld = metaOffset + metaSize;
        var afterNew = metaOffset + newInnerFile.Length;
        Array.Copy(asyncData, afterOld, newAsyncData, afterNew, asyncData.Length - afterOld);

        var preAsyncSize = asyncStart;
        var result = new byte[preAsyncSize + newAsyncData.Length];
        Array.Copy(originalBundle, 0, result, 0, preAsyncSize);

        if (sizeDiff != 0)
        {
            UpdateFileTableEntry(result, headerEnd, defSize, metaOffset, newInnerFile.Length, sizeDiff);
        }

        BitConverter.GetBytes((uint)newAsyncData.Length).CopyTo(result, 12);

        Array.Copy(newAsyncData, 0, result, asyncStart, newAsyncData.Length);

        return result;
    }

    private static (int offset, int size) FindFileInFileTable(byte[] data, int headerEnd, int defSize, string targetName)
    {
        var fileCount = BitConverter.ToUInt32(data, headerEnd + 4);
        var p = 8;

        for (uint i = 0; i < fileCount && p + 8 <= defSize; i++)
        {
            var offset = BitConverter.ToInt32(data, headerEnd + p); p += 4;
            var size = BitConverter.ToInt32(data, headerEnd + p); p += 4;

            var nameStart = p;
            while (p < defSize && data[headerEnd + p] != 0) p++;
            var nameLen = p - nameStart;
            p++;
            var padded = ((nameLen + 1) + 3) & ~3;
            p += padded - (nameLen + 1);
            p += 16;

            if (nameLen > 0)
            {
                var nameBytes = data.AsSpan(headerEnd + nameStart, nameLen);
                bool allPrintable = true;
                foreach (var b in nameBytes)
                {
                    if (b < 0x20 || b >= 0x7F) { allPrintable = false; break; }
                }
                if (allPrintable)
                {
                    var name = System.Text.Encoding.ASCII.GetString(nameBytes);
                    if (name == targetName)
                        return (offset, size);
                }
            }
        }

        return (-1, -1);
    }

    private static (int offset, int size) FindMetadataInFileTable(byte[] data, int headerEnd, int defSize)
    {
        var fileCount = BitConverter.ToUInt32(data, headerEnd + 4);
        var p = 8;

        for (uint i = 0; i < fileCount && p + 8 <= defSize; i++)
        {
            var offset = BitConverter.ToInt32(data, headerEnd + p); p += 4;
            var size = BitConverter.ToInt32(data, headerEnd + p); p += 4;

            var nameStart = p;
            while (p < defSize && data[headerEnd + p] != 0) p++;
            var nameLen = p - nameStart;
            p++;
            var padded = ((nameLen + 1) + 3) & ~3;
            p += padded - (nameLen + 1);
            p += 16;

            if (nameLen > 0)
            {
                var nameBytes = data.AsSpan(headerEnd + nameStart, nameLen);
                var isReadable = nameBytes.Length > 0 && nameLen > 0;
                if (isReadable)
                {
                    bool allPrintable = true;
                    foreach (var b in nameBytes)
                    {
                        if (b < 0x20 || b >= 0x7F) { allPrintable = false; break; }
                    }
                    if (allPrintable)
                    {
                        var name = System.Text.Encoding.ASCII.GetString(nameBytes);
                        if (name is "metadata_save.p" or "metadata_slot.p")
                            return (offset, size);
                    }
                }
            }
        }

        return (-1, -1);
    }

    private static void UpdateFileTableEntry(byte[] data, int headerEnd, int defSize,
        int metaOffset, int newMetaSize, int sizeDiff)
    {
        var fileCount = BitConverter.ToUInt32(data, headerEnd + 4);
        var p = 8;

        for (uint i = 0; i < fileCount && p + 8 <= defSize; i++)
        {
            var offset = BitConverter.ToInt32(data, headerEnd + p);

            if (offset == metaOffset)
            {
                BitConverter.GetBytes(newMetaSize).CopyTo(data, headerEnd + p + 4);
            }
            else if (offset > metaOffset)
            {
                BitConverter.GetBytes(offset + sizeDiff).CopyTo(data, headerEnd + p);
            }

            p += 4;
            p += 4;

            while (p < defSize && data[headerEnd + p] != 0) p++;
            p++;
            p = ((p + 3) / 4) * 4;
            p += 16;
        }
    }

    private static byte[] DecompressSection(byte[] rawBytes)
    {
        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
            return DecompressTtcz(rawBytes);

        try
        {
            using var compStream = new MemoryStream(rawBytes);
            using var zlib = new ZLibStream(compStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            using var compStream = new MemoryStream(rawBytes);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }

    private static byte[] DecompressTtcz(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        br.ReadUInt32();
        var windowSize = br.ReadUInt32();
        var pageCount = br.ReadUInt32();

        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++)
            offsets[i] = br.ReadUInt64();

        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            var compStart = (long)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            ms.Position = compStart;
            var compressedBlock = br.ReadBytes(compLen);

            using var compStream = new MemoryStream(compressedBlock);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            var pageBuffer = new byte[windowSize];
            int totalRead = 0;
            int bytesRead;
            while ((bytesRead = deflate.Read(pageBuffer, totalRead, (int)windowSize - totalRead)) > 0)
                totalRead += bytesRead;
            output.Write(pageBuffer, 0, totalRead);
        }

        return output.ToArray();
    }

    private static byte[] RebuildInnerMetaStream(byte[] originalInner, byte[] newPropData)
    {
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
            versionEntries.Add((origReader.ReadUInt64(), origReader.ReadUInt32()));
        }

        var rawDefSize = (int)(origDefSize & 0x7FFFFFFF);
        var rawDbgSize = (int)(origDbgSize & 0x7FFFFFFF);
        var rawAsyncSize = (int)(origAsyncSize & 0x7FFFFFFF);
        origReader.ReadBytes(rawDefSize);
        var origDbgData = origReader.ReadBytes(rawDbgSize);
        var origAsyncData = origReader.ReadBytes(rawAsyncSize);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);
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
}
