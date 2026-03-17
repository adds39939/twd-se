using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Creates new estore and epage files from scratch for S3/Michonne saves.
/// Builds the MSV6 MetaStream wrapper and internal structures matching the
/// binary format found in real Definitive Edition save files.
/// </summary>
public static class EStoreCreator
{
    private const int StandardPageNumber = 734;
    private const uint BlockSize = 0x00000020;

    // MSV6 version entries shared by estore/epage files
    private static readonly (ulong TypeCrc, uint VersionCrc)[] VersionEntries =
    [
        (0xCD75DC4F6B9F15D2, 0x21F2BCC9),
        (0x84283CB979D71641, 0x0527D6BF),
        (0x004F023463D89FB0, 0xB539B0FF),
    ];

    /// <summary>
    /// Create a new estore + epage file pair.
    /// Returns (estoreBytes, epageBytes, epageFilename).
    /// </summary>
    public static (byte[] estore, byte[] epage, string epageFilename) Create(
        string slotBaseName,
        List<EventLogEntry> events)
    {
        var estoreFilename = $"{slotBaseName}_id.estore";
        var epageFilename = $"{slotBaseName}_id_Page{StandardPageNumber}.epage";

        var epageDefaultSection = BuildEpageDefaultSection(epageFilename, events);
        var epageBytes = WrapInMsv6(epageDefaultSection);

        var estoreDefaultSection = BuildEstoreDefaultSection(estoreFilename, StandardPageNumber);
        var estoreBytes = WrapInMsv6(estoreDefaultSection);

        return (estoreBytes, epageBytes, epageFilename);
    }

    /// <summary>
    /// Build a single 42-byte event record. Delegates to EStoreWriter.BuildRecord.
    /// </summary>
    public static byte[] BuildRecord(EventLogEntry entry) => EStoreWriter.BuildRecord(entry);

    /// <summary>
    /// Wrap a default section in an MSV6 MetaStream envelope.
    /// </summary>
    private static byte[] WrapInMsv6(byte[] defaultSectionData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(MetaStreamHeader.MagicMsv6);       // magic
        writer.WriteUInt32((uint)defaultSectionData.Length);    // defaultSectionSize (uncompressed)
        writer.WriteUInt32(0);                                 // debugSectionSize
        writer.WriteUInt32(0);                                 // asyncSectionSize
        writer.WriteUInt32((uint)VersionEntries.Length);        // versionCount

        foreach (var (typeCrc, versionCrc) in VersionEntries)
        {
            writer.WriteUInt64(typeCrc);
            writer.WriteUInt32(versionCrc);
        }

        writer.WriteBytes(defaultSectionData);
        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Build the default section for an epage file.
    /// Structure: flags(4) + pageTypeHash(8) + blockSize(4) + filenameLength(4) + filename + event records.
    /// </summary>
    private static byte[] BuildEpageDefaultSection(string filename, List<EventLogEntry> events)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        var filenameBytes = Encoding.ASCII.GetBytes(filename);

        writer.WriteUInt32(0x00000000);                // flags
        writer.WriteUInt64(0);                         // pageTypeHash (zero for new files)
        writer.WriteUInt32(BlockSize);                 // blockSize
        writer.WriteUInt32((uint)filenameBytes.Length); // filenameLength
        writer.WriteBytes(filenameBytes);               // filename

        // Write event records
        for (int i = 0; i < events.Count; i++)
        {
            var entry = events[i];
            entry.SequenceIndex = (uint)i;
            writer.WriteBytes(EStoreWriter.BuildRecord(entry));
        }

        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Build the default section for an estore file.
    /// Structure: flags(4) + estoreTypeHash(8) + indexTotalSize(4) + pageEntryCount(4) +
    ///            page entries + blockSize(4) + filenameLength(4) + filename.
    /// </summary>
    private static byte[] BuildEstoreDefaultSection(string filename, int pageNumber)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        var filenameBytes = Encoding.ASCII.GetBytes(filename);

        // Each page entry: u32 entrySize(0x0C) + u64 pageHash + u32 pageNumber = 16 bytes
        const int pageEntrySize = 16;
        const int pageEntryCount = 1;
        // indexTotalSize = pageEntryCount(4) + pageEntries(16 * count)
        int indexTotalSize = 4 + pageEntrySize * pageEntryCount;

        writer.WriteUInt32(0x00000000);                // flags
        writer.WriteUInt64(0);                         // estoreTypeHash (zero for new files)
        writer.WriteUInt32((uint)indexTotalSize);       // indexTotalSize
        writer.WriteUInt32(pageEntryCount);            // pageEntryCount

        // Page entry
        writer.WriteUInt32(0x0C);                      // entrySize (12 bytes of payload: 8 hash + 4 number)
        writer.WriteUInt64(0);                         // pageHash (zero for new files)
        writer.WriteUInt32((uint)pageNumber);          // pageNumber

        writer.WriteUInt32(BlockSize);                 // blockSize
        writer.WriteUInt32((uint)filenameBytes.Length); // filenameLength
        writer.WriteBytes(filenameBytes);               // filename

        // No overflow event records for new files

        writer.Flush();
        return ms.ToArray();
    }
}
