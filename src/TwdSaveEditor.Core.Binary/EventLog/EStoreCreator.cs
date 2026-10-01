using System.Text;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.EventLog;

public static class EStoreCreator
{
    private const int StandardPageNumber = 734;
    private const uint BlockSize = 0x00000020;

    private static readonly (ulong TypeCrc, uint VersionCrc)[] VersionEntries =
    [
        (0x3AAEB61240D3CFBA, 0xD8D22CB9),
        (0xBEBB886A0541595F, 0xB59B0682),
        (0x004F023463D89FB0, 0xB539B0FF),
        (0x24032A7AD8BB721D, 0x739CE237),
        (0x238A520C4A924AA6, 0x2E4AF103),
    ];

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

    public static byte[] BuildRecord(EventLogEntry entry) => EStoreWriter.BuildRecord(entry);

    private static byte[] WrapInMsv6(byte[] defaultSectionData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(MetaStreamHeader.MagicMsv6);
        writer.WriteUInt32((uint)defaultSectionData.Length);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)VersionEntries.Length);

        foreach (var (typeCrc, versionCrc) in VersionEntries)
        {
            writer.WriteUInt64(typeCrc);
            writer.WriteUInt32(versionCrc);
        }

        writer.WriteBytes(defaultSectionData);
        writer.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildEpageDefaultSection(string filename, List<EventLogEntry> events)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        var filenameBytes = Encoding.ASCII.GetBytes(filename);

        writer.WriteUInt32(0x00000000);
        writer.WriteUInt64(0);
        writer.WriteUInt32(BlockSize);
        writer.WriteUInt32((uint)filenameBytes.Length);
        writer.WriteBytes(filenameBytes);

        for (int i = 0; i < events.Count; i++)
        {
            var entry = events[i];
            entry.SequenceIndex = (uint)i;
            writer.WriteBytes(EStoreWriter.BuildRecord(entry));
        }

        writer.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildEstoreDefaultSection(string filename, int pageNumber)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        var filenameBytes = Encoding.ASCII.GetBytes(filename);

        const int pageEntrySize = 16;
        const int pageEntryCount = 1;
        int indexTotalSize = 4 + pageEntrySize * pageEntryCount;

        writer.WriteUInt32(0x00000000);
        writer.WriteUInt64(0);
        writer.WriteUInt32((uint)indexTotalSize);
        writer.WriteUInt32(pageEntryCount);

        writer.WriteUInt32(0x0C);
        writer.WriteUInt64(0);
        writer.WriteUInt32((uint)pageNumber);

        writer.WriteUInt32(BlockSize);
        writer.WriteUInt32((uint)filenameBytes.Length);
        writer.WriteBytes(filenameBytes);

        writer.Flush();
        return ms.ToArray();
    }
}
