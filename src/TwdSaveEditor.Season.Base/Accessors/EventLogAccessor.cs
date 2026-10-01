using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.EventLog;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Accessors;

public sealed class EventLogAccessor : IChoiceAccessor
{
    private readonly SaveSlot _slot;
    private readonly ChoiceNodeMap _nodes;
    private List<TrackedEventLogEntry>? _cachedEntries;
    private HashSet<ulong>? _dialogNodeHashes;

    public EventLogAccessor(SaveSlot slot, ChoiceNodeMap nodes)
    {
        _slot = slot;
        _nodes = nodes;
    }

    public bool HasEventLog => _slot.LoadedEventLogEntries != null ||
                               (_slot.EStorePath != null && File.Exists(_slot.EStorePath));

    public string? GetChoiceValue(string choiceKey)
    {
        EnsureHashIndex();

        foreach (var (hash, (key, val)) in _nodes.Nodes)
        {
            if (key == choiceKey && _dialogNodeHashes!.Contains(hash))
                return val;
        }

        return null;
    }

    public void SetChoiceValue(string choiceKey, string value)
    {
        var entries = GetEntries();

        var targetHash = _nodes.GetNodeHash(choiceKey, value);
        if (!targetHash.HasValue)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            var tracked = entries[i];
            if (!tracked.Entry.IsDialogNode)
                continue;

            var detected = _nodes.DetectChoice(tracked.Entry.NodeHash);
            if (detected.HasValue && detected.Value.ChoiceKey == choiceKey)
            {
                _dialogNodeHashes?.Remove(tracked.Entry.NodeHash);
                tracked.Entry.NodeHash = targetHash.Value;
                _dialogNodeHashes?.Add(targetHash.Value);

                if (tracked.Entry.RawData.Length >= 37)
                    BitConverter.GetBytes(targetHash.Value).CopyTo(tracked.Entry.RawData, 29);

                if (tracked.SourcePath != "memory")
                    EStoreWriter.WriteEntryToPage(tracked.SourcePath, tracked.RecordIndex, tracked.Entry);
                return;
            }
        }

        AddNewEntry(targetHash.Value, entries);
    }

    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        var currentValue = GetChoiceValue(choice.ChoiceKey);
        if (currentValue == null)
            return -1;

        for (int i = 0; i < choice.Options.Length; i++)
        {
            if (choice.Options[i].Value.Equals(currentValue, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex)
    {
        var opt = choice.Options[optionIndex];
        SetChoiceValue(choice.ChoiceKey, opt.Value);
    }

    public List<(string Key, string Value)> GetAllChoices()
    {
        EnsureHashIndex();
        var result = new List<(string, string)>();
        var seen = new HashSet<string>();

        foreach (var (hash, (key, val)) in _nodes.Nodes)
        {
            if (_dialogNodeHashes!.Contains(hash) && seen.Add(key))
                result.Add((key, val));
        }

        return result;
    }

    public void InvalidateCache()
    {
        _cachedEntries = null;
        _dialogNodeHashes = null;
    }

    private void EnsureHashIndex()
    {
        if (_dialogNodeHashes != null) return;
        var entries = GetEntries();
        _dialogNodeHashes = new HashSet<ulong>();
        foreach (var tracked in entries)
        {
            if (tracked.Entry.IsDialogNode)
                _dialogNodeHashes.Add(tracked.Entry.NodeHash);
        }
    }

    private List<TrackedEventLogEntry> GetEntries()
    {
        if (_cachedEntries != null)
            return _cachedEntries;

        _cachedEntries = [];

        if (_slot.LoadedEventLogEntries != null)
        {
            for (int i = 0; i < _slot.LoadedEventLogEntries.Count; i++)
            {
                _cachedEntries.Add(new TrackedEventLogEntry
                {
                    Entry = _slot.LoadedEventLogEntries[i],
                    SourcePath = "memory",
                    RecordIndex = i,
                });
            }
            return _cachedEntries;
        }

        if (_slot.EStorePath == null || !File.Exists(_slot.EStorePath))
            return _cachedEntries;

        ReadEntriesFromFile(_slot.EStorePath);

        if (_slot.EPagePaths != null)
        {
            foreach (var epagePath in _slot.EPagePaths.OrderBy(EventLogFiles.ExtractPageNumber))
            {
                if (File.Exists(epagePath))
                    ReadEntriesFromFile(epagePath);
            }
        }

        return _cachedEntries;
    }

    private void ReadEntriesFromFile(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        var sections = EStoreReader.ReadMetaStreamSections(data);
        if (sections.defaultData.Length == 0)
            return;

        var fileEntries = EStoreReader.ParseEventsFromSection(sections.defaultData);
        for (int i = 0; i < fileEntries.Count; i++)
        {
            _cachedEntries!.Add(new TrackedEventLogEntry
            {
                Entry = fileEntries[i],
                SourcePath = filePath,
                RecordIndex = i,
            });
        }
    }

    private void AddNewEntry(ulong nodeHash, List<TrackedEventLogEntry> entries)
    {
        _dialogNodeHashes?.Add(nodeHash);

        if (_slot.LoadedEventLogEntries != null)
        {
            uint memSeq = 1;
            if (entries.Count > 0)
                memSeq = entries.Max(e => e.Entry.SequenceIndex) + 1;

            var memEntry = new EventLogEntry
            {
                EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
                NodeHash = nodeHash,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = memSeq,
                Trailing = 0,
                RawData = new byte[EventLogEntry.RecordSize],
            };
            var memRecord = EStoreWriter.BuildRecord(memEntry);
            Array.Copy(memRecord, memEntry.RawData, EventLogEntry.RecordSize);

            _slot.LoadedEventLogEntries.Add(memEntry);
            _cachedEntries?.Add(new TrackedEventLogEntry
            {
                Entry = memEntry,
                SourcePath = "memory",
                RecordIndex = _slot.LoadedEventLogEntries.Count - 1,
            });
            return;
        }

        string targetPath;
        if (_slot.EPagePaths != null && _slot.EPagePaths.Count > 0)
            targetPath = _slot.EPagePaths.OrderBy(EventLogFiles.ExtractPageNumber).Last();
        else if (_slot.EStorePath != null)
            targetPath = _slot.EStorePath;
        else
            return;

        uint nextSeq = 1;
        if (entries.Count > 0)
            nextSeq = entries.Max(e => e.Entry.SequenceIndex) + 1;

        var newEntry = new EventLogEntry
        {
            EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
            NodeHash = nodeHash,
            ValueType = 1,
            ExtraFlag = 0,
            SequenceIndex = nextSeq,
            Trailing = 0,
            RawData = new byte[EventLogEntry.RecordSize],
        };

        var record = EStoreWriter.BuildRecord(newEntry);
        Array.Copy(record, newEntry.RawData, EventLogEntry.RecordSize);

        var fileData = File.ReadAllBytes(targetPath);
        var sections = EStoreReader.ReadMetaStreamSections(fileData);
        var defaultData = sections.defaultData;

        var newDefault = new byte[defaultData.Length + EventLogEntry.RecordSize];
        Array.Copy(defaultData, newDefault, defaultData.Length);
        Array.Copy(record, 0, newDefault, defaultData.Length, EventLogEntry.RecordSize);

        var rebuilt = RebuildMetaStreamWithDefault(fileData, newDefault);
        File.WriteAllBytes(targetPath, rebuilt);

        var recordIndex = EStoreReader.ParseEventsFromSection(defaultData).Count;
        _cachedEntries?.Add(new TrackedEventLogEntry
        {
            Entry = newEntry,
            SourcePath = targetPath,
            RecordIndex = recordIndex,
        });
    }

    private static byte[] RebuildMetaStreamWithDefault(byte[] original, byte[] newDefaultData)
    {
        using var origMs = new MemoryStream(original);
        using var origReader = new BinaryReaderEx(origMs);

        var magic = origReader.ReadUInt32();
        var origDefSize = origReader.ReadUInt32();
        var origDbgSize = origReader.ReadUInt32();
        var origAsyncSize = origReader.ReadUInt32();
        var verCount = origReader.ReadUInt32();

        var versionEntries = new List<(ulong TypeCrc, uint VersionCrc)>();
        for (uint i = 0; i < verCount; i++)
        {
            var tc = origReader.ReadUInt64();
            var vc = origReader.ReadUInt32();
            versionEntries.Add((tc, vc));
        }

        var origDefRawSize = (int)(origDefSize & 0x7FFFFFFF);
        origReader.ReadBytes(origDefRawSize);

        var dbgRawSize = (int)(origDbgSize & 0x7FFFFFFF);
        var dbgData = dbgRawSize > 0 ? origReader.ReadBytes(dbgRawSize) : Array.Empty<byte>();

        var asyncRawSize = (int)(origAsyncSize & 0x7FFFFFFF);
        var asyncData = asyncRawSize > 0 ? origReader.ReadBytes(asyncRawSize) : Array.Empty<byte>();

        using var outMs = new MemoryStream();
        using var writer = new BinaryWriterEx(outMs, leaveOpen: true);

        writer.WriteUInt32(magic);
        writer.WriteUInt32((uint)newDefaultData.Length);
        writer.WriteUInt32(origDbgSize);
        writer.WriteUInt32(origAsyncSize);
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
}
