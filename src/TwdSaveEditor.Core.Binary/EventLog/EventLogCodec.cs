using System.Text;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.EventLog;

public static class EventLogCodec
{
    private const int DebugBytesPerSymbol = 4;
    private const int BlockPrefix = sizeof(uint);
    private const int HandleBlockSize = BlockPrefix + sizeof(ulong);
    private const byte True = (byte)'1';
    private const byte False = (byte)'0';

    private const uint StorageVersion = 0xD8D22CB9;
    private const uint PageEntryVersion = 0xB59B0682;
    private const uint SymbolVersion = 0xB539B0FF;
    private const uint PageVersion = 0x739CE237;
    private const uint EventVersion = 0x2E4AF103;

    private static readonly ulong StorageType = TelltaleHash.ComputeCrc64("EventStorage");
    private static readonly ulong PageEntryType = TelltaleHash.ComputeCrc64("EventStorage::PageEntry");
    private static readonly ulong PageType = TelltaleHash.ComputeCrc64("EventStoragePage");
    private static readonly ulong EventType = TelltaleHash.ComputeCrc64("EventLoggerEvent");

    public static EventLogStorage ReadStorage(byte[] file)
    {
        var content = MetaStreamCodec.Read(file);
        using var reader = new BinaryReader(new MemoryStream(content.Default));

        var storage = new EventLogStorage
        {
            Version = reader.ReadInt32(),
            SessionId = reader.ReadUInt64(),
            VersionEntries = content.Header.VersionEntries,
        };

        reader.ReadUInt32();
        for (var count = reader.ReadUInt32(); count > 0; count--)
        {
            reader.ReadUInt32();
            storage.Pages.Add(new EventLogPageEntry(reader.ReadUInt64(), reader.ReadUInt32()));
        }

        storage.Name = ReadString(reader);
        storage.LastEventId = reader.ReadUInt32();
        storage.PageSize = reader.ReadInt32();
        if (reader.ReadByte() == True)
            storage.CurrentPage = ReadPageBody(reader);

        EnsureConsumed(reader, storage.Name);
        return storage;
    }

    public static byte[] WriteStorage(EventLogStorage storage)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write(storage.Version);
            writer.Write(storage.SessionId);
            writer.Write((uint)(BlockPrefix + sizeof(uint) + storage.Pages.Count * (HandleBlockSize + sizeof(uint))));
            writer.Write(storage.Pages.Count);
            foreach (var page in storage.Pages)
            {
                writer.Write((uint)HandleBlockSize);
                writer.Write(page.PageSymbol);
                writer.Write(page.MaxEventId);
            }

            WriteString(writer, storage.Name);
            writer.Write(storage.LastEventId);
            writer.Write(storage.PageSize);
            writer.Write(storage.CurrentPage == null ? False : True);
            if (storage.CurrentPage != null)
                WritePageBody(writer, storage.CurrentPage);
        }

        var symbols = storage.Pages.Count + (storage.CurrentPage == null ? 0 : CountSymbols(storage.CurrentPage));
        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries = storage.VersionEntries.Count > 0 ? storage.VersionEntries : DefaultStorageVersions(),
        };

        return MetaStreamCodec.Write(new MetaStreamContent(header, stream.ToArray(), new byte[symbols * DebugBytesPerSymbol], []));
    }

    public static EventLogPage ReadPage(byte[] file)
    {
        var content = MetaStreamCodec.Read(file);
        using var reader = new BinaryReader(new MemoryStream(content.Default));
        var page = ReadPageBody(reader);
        page.VersionEntries = content.Header.VersionEntries;
        EnsureConsumed(reader, page.FlushedName);
        return page;
    }

    public static byte[] WritePage(EventLogPage page)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
            WritePageBody(writer, page);

        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries = page.VersionEntries.Count > 0 ? page.VersionEntries : DefaultPageVersions(),
        };

        return MetaStreamCodec.Write(new MetaStreamContent(header, stream.ToArray(), new byte[CountSymbols(page) * DebugBytesPerSymbol], []));
    }

    private static EventLogPage ReadPageBody(BinaryReader reader)
    {
        var page = new EventLogPage
        {
            Version = reader.ReadInt32(),
            SessionId = reader.ReadUInt64(),
            FlushedName = ReadString(reader),
        };

        for (var count = reader.ReadUInt32(); count > 0; count--)
            page.Events.Add(ReadEvent(reader));

        return page;
    }

    private static void WritePageBody(BinaryWriter writer, EventLogPage page)
    {
        writer.Write(page.Version);
        writer.Write(page.SessionId);
        WriteString(writer, page.FlushedName);
        writer.Write(page.Events.Count);
        foreach (var entry in page.Events)
            WriteEvent(writer, entry);
    }

    private static EventLogEvent ReadEvent(BinaryReader reader)
    {
        var entry = new EventLogEvent { Id = reader.ReadUInt32(), MaxSeverity = reader.ReadInt32() };
        var end = reader.BaseStream.Position + reader.ReadUInt32();
        var typeCount = reader.ReadUInt32();
        entry.ChildCount = reader.ReadUInt32();

        var kinds = new List<byte[]>();
        for (uint i = 0; i < typeCount; i++)
        {
            entry.Data.Add(new EventLogData(reader.ReadUInt64(), []));
            kinds.Add(reader.ReadBytes(reader.ReadInt32()));
        }

        for (var i = 0; i < entry.Data.Count; i++)
        {
            foreach (var kind in kinds[i])
                entry.Data[i].Values.Add(new EventLogValue(kind, reader.ReadUInt64(), reader.ReadByte()));
        }

        if (reader.BaseStream.Position != end)
            throw new InvalidDataException($"Event {entry.Id} does not end where its block says.");

        return entry;
    }

    private static void WriteEvent(BinaryWriter writer, EventLogEvent entry)
    {
        writer.Write(entry.Id);
        writer.Write(entry.MaxSeverity);

        var values = entry.Data.Sum(data => data.Values.Count);
        var size = BlockPrefix + 2 * sizeof(uint) + entry.Data.Count * (sizeof(ulong) + sizeof(int)) + values * (1 + sizeof(ulong) + 1);
        writer.Write((uint)size);
        writer.Write(entry.Data.Count);
        writer.Write(entry.ChildCount);
        foreach (var data in entry.Data)
        {
            writer.Write(data.Type);
            writer.Write(data.Values.Count);
            foreach (var value in data.Values)
                writer.Write(value.Kind);
        }

        foreach (var value in entry.Data.SelectMany(data => data.Values))
        {
            writer.Write(value.Raw);
            writer.Write(value.Severity);
        }
    }

    private static int CountSymbols(EventLogPage page) =>
        page.Events.Sum(entry => entry.Data.Count + entry.Data.Sum(data => data.Values.Count(value => value.IsSymbol)));

    private static string ReadString(BinaryReader reader)
    {
        reader.ReadUInt32();
        return Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadInt32()));
    }

    private static void WriteString(BinaryWriter writer, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        writer.Write((uint)(BlockPrefix + sizeof(int) + bytes.Length));
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void EnsureConsumed(BinaryReader reader, string name)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException($"Event log file {name} has unread data.");
    }

    private static List<VersionEntry> DefaultStorageVersions() =>
    [
        new(StorageType, StorageVersion),
        new(PageEntryType, PageEntryVersion),
        new(TelltaleTypes.Symbol, SymbolVersion),
        new(PageType, PageVersion),
        new(EventType, EventVersion),
    ];

    private static List<VersionEntry> DefaultPageVersions() =>
    [
        new(PageType, PageVersion),
        new(EventType, EventVersion),
        new(TelltaleTypes.Symbol, SymbolVersion),
    ];
}
