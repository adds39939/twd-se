using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// High-level accessor for reading/writing game choices from estore/epage-based saves.
/// Used by S3 and Michonne, which store choices as "Executing Dialog Node" events
/// in EventLog records rather than in choices.prop string arrays.
/// </summary>
public sealed class EventLogAccessor : IChoiceAccessor
{
    private readonly SaveSlot _slot;
    private readonly string _seasonKey;
    private List<TrackedEntry>? _cachedEntries;

    /// <summary>
    /// An EventLog entry along with the source file it was read from,
    /// so we can write modifications back to the correct epage file.
    /// </summary>
    private sealed class TrackedEntry
    {
        public required EventLogEntry Entry { get; init; }

        /// <summary>Source file path (estore or epage) this entry came from.</summary>
        public required string SourcePath { get; init; }

        /// <summary>Zero-based index of this record within the source file's record block.</summary>
        public required int RecordIndex { get; init; }
    }

    public EventLogAccessor(SaveSlot slot)
    {
        _slot = slot;
        _seasonKey = slot.DetectedSeasonKey ?? "s3";
    }

    /// <summary>Whether estore/epage files exist for this save slot.</summary>
    public bool HasEventLog => _slot.LoadedEventLogEntries != null ||
                               (_slot.EStorePath != null && File.Exists(_slot.EStorePath));

    /// <summary>
    /// Get the current value of a choice key by scanning EventLog entries for matching node hashes.
    /// Returns null if the choice is not found in the EventLog.
    /// </summary>
    public string? GetChoiceValue(string choiceKey)
    {
        var entries = GetEntries();

        if (_seasonKey == "michonne")
        {
            // Michonne uses GUIDs, but EventLog still stores CRC64 hashes of the GUID strings.
            // We need to check each known GUID's CRC64 against the EventLog entries.
            foreach (var tracked in entries)
            {
                if (!tracked.Entry.IsDialogNode)
                    continue;

                foreach (var (guid, (key, val)) in ChoiceNodeMapping.MichonneNodes)
                {
                    if (key != choiceKey)
                        continue;

                    // The NodeHash in the EventLog is the CRC64 of the GUID string
                    var guidHash = Hashing.TelltaleHash.ComputeCrc64(guid);
                    if (tracked.Entry.NodeHash == guidHash)
                        return val;
                }
            }
            return null;
        }

        // S3/S4: Direct CRC64 hash lookup
        foreach (var tracked in entries)
        {
            if (!tracked.Entry.IsDialogNode)
                continue;

            var detected = ChoiceNodeMapping.DetectChoice(_seasonKey, tracked.Entry.NodeHash);
            if (detected.HasValue && detected.Value.ChoiceKey == choiceKey)
                return detected.Value.OptionValue;
        }

        return null;
    }

    /// <summary>
    /// Set a choice value by finding the existing EventLog entry for the choice
    /// and replacing its node hash with the new option's hash.
    /// If no existing entry is found, a new entry is appended to the last epage file.
    /// </summary>
    public void SetChoiceValue(string choiceKey, string value)
    {
        var entries = GetEntries();

        if (_seasonKey == "michonne")
        {
            SetMichonneChoiceValue(choiceKey, value, entries);
            return;
        }

        // Get the target hash for the new value
        var targetHash = ChoiceNodeMapping.GetNodeHash(_seasonKey, choiceKey, value);
        if (!targetHash.HasValue)
            return; // No mapping available for this choice option

        // Find existing entry for this choice key
        for (int i = 0; i < entries.Count; i++)
        {
            var tracked = entries[i];
            if (!tracked.Entry.IsDialogNode)
                continue;

            var detected = ChoiceNodeMapping.DetectChoice(_seasonKey, tracked.Entry.NodeHash);
            if (detected.HasValue && detected.Value.ChoiceKey == choiceKey)
            {
                // Replace the node hash
                tracked.Entry.NodeHash = targetHash.Value;

                // Update the raw data
                if (tracked.Entry.RawData.Length >= 37)
                    BitConverter.GetBytes(targetHash.Value).CopyTo(tracked.Entry.RawData, 29);

                // Write back to the source epage file (skip in WASM/memory mode)
                if (tracked.SourcePath != "memory")
                    EStoreWriter.WriteEntryToPage(tracked.SourcePath, tracked.RecordIndex, tracked.Entry);
                return;
            }
        }

        // No existing entry found -- add a new one to the last epage file
        AddNewEntry(targetHash.Value, entries);
    }

    /// <summary>
    /// Detect which option index matches the current state for a choice definition.
    /// Returns -1 if the choice key is not found in the EventLog.
    /// </summary>
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

    /// <summary>
    /// Apply a choice option, writing its value to the EventLog.
    /// </summary>
    public void ApplyChoice(ChoiceDefinition choice, int optionIndex)
    {
        var opt = choice.Options[optionIndex];
        SetChoiceValue(choice.ChoiceKey, opt.Value);
    }

    /// <summary>
    /// Get all detected choices from the EventLog as key-value pairs.
    /// </summary>
    public List<(string Key, string Value)> GetAllChoices()
    {
        var result = new List<(string, string)>();
        var entries = GetEntries();
        var seen = new HashSet<string>();

        if (_seasonKey == "michonne")
        {
            foreach (var tracked in entries)
            {
                if (!tracked.Entry.IsDialogNode)
                    continue;

                foreach (var (guid, (key, val)) in ChoiceNodeMapping.MichonneNodes)
                {
                    var guidHash = Hashing.TelltaleHash.ComputeCrc64(guid);
                    if (tracked.Entry.NodeHash == guidHash && seen.Add(key))
                    {
                        result.Add((key, val));
                        break;
                    }
                }
            }
            return result;
        }

        foreach (var tracked in entries)
        {
            if (!tracked.Entry.IsDialogNode)
                continue;

            var detected = ChoiceNodeMapping.DetectChoice(_seasonKey, tracked.Entry.NodeHash);
            if (detected.HasValue && seen.Add(detected.Value.ChoiceKey))
                result.Add((detected.Value.ChoiceKey, detected.Value.OptionValue));
        }

        return result;
    }

    /// <summary>
    /// Invalidate the cached entries so they are re-read from disk on next access.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedEntries = null;
    }

    // ── Private helpers ───────────────────────────────────────────────

    private List<TrackedEntry> GetEntries()
    {
        if (_cachedEntries != null)
            return _cachedEntries;

        _cachedEntries = [];

        // WASM mode: use pre-loaded entries
        if (_slot.LoadedEventLogEntries != null)
        {
            for (int i = 0; i < _slot.LoadedEventLogEntries.Count; i++)
            {
                _cachedEntries.Add(new TrackedEntry
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

        // Read from estore file
        ReadEntriesFromFile(_slot.EStorePath);

        // Read from epage files
        if (_slot.EPagePaths != null)
        {
            foreach (var epagePath in _slot.EPagePaths.OrderBy(ExtractPageNumber))
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
            _cachedEntries!.Add(new TrackedEntry
            {
                Entry = fileEntries[i],
                SourcePath = filePath,
                RecordIndex = i,
            });
        }
    }

    private void SetMichonneChoiceValue(string choiceKey, string value, List<TrackedEntry> entries)
    {
        // Get the target GUID
        var targetGuid = ChoiceNodeMapping.GetMichonneGuid(choiceKey, value);
        if (targetGuid == null)
            return;

        var targetHash = Hashing.TelltaleHash.ComputeCrc64(targetGuid);

        // Find existing entry for this choice key
        for (int i = 0; i < entries.Count; i++)
        {
            var tracked = entries[i];
            if (!tracked.Entry.IsDialogNode)
                continue;

            foreach (var (guid, (key, _)) in ChoiceNodeMapping.MichonneNodes)
            {
                if (key != choiceKey)
                    continue;

                var guidHash = Hashing.TelltaleHash.ComputeCrc64(guid);
                if (tracked.Entry.NodeHash == guidHash)
                {
                    // Replace the node hash
                    tracked.Entry.NodeHash = targetHash;
                    if (tracked.Entry.RawData.Length >= 37)
                        BitConverter.GetBytes(targetHash).CopyTo(tracked.Entry.RawData, 29);
                    if (tracked.SourcePath != "memory")
                        EStoreWriter.WriteEntryToPage(tracked.SourcePath, tracked.RecordIndex, tracked.Entry);
                    return;
                }
            }
        }

        // No existing entry found -- add new
        AddNewEntry(targetHash, entries);
    }

    private void AddNewEntry(ulong nodeHash, List<TrackedEntry> entries)
    {
        // WASM/memory mode: just add to the in-memory list
        if (_slot.LoadedEventLogEntries != null)
        {
            uint memSeq = 1;
            if (entries.Count > 0)
                memSeq = entries.Max(e => e.Entry.SequenceIndex) + 1;

            var memEntry = new EventLogEntry
            {
                EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
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
            _cachedEntries?.Add(new TrackedEntry
            {
                Entry = memEntry,
                SourcePath = "memory",
                RecordIndex = _slot.LoadedEventLogEntries.Count - 1,
            });
            return;
        }

        // Determine the target file: last epage, or estore if no epages exist
        string targetPath;
        if (_slot.EPagePaths != null && _slot.EPagePaths.Count > 0)
            targetPath = _slot.EPagePaths.OrderBy(ExtractPageNumber).Last();
        else if (_slot.EStorePath != null)
            targetPath = _slot.EStorePath;
        else
            return;

        // Determine next sequence index
        uint nextSeq = 1;
        if (entries.Count > 0)
            nextSeq = entries.Max(e => e.Entry.SequenceIndex) + 1;

        var newEntry = new EventLogEntry
        {
            EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
            NodeHash = nodeHash,
            ValueType = 1,
            ExtraFlag = 0,
            SequenceIndex = nextSeq,
            Trailing = 0,
            RawData = new byte[EventLogEntry.RecordSize],
        };

        // Build raw data
        var record = EStoreWriter.BuildRecord(newEntry);
        Array.Copy(record, newEntry.RawData, EventLogEntry.RecordSize);

        // Read existing file, append record to the default section, write back
        var fileData = File.ReadAllBytes(targetPath);
        var sections = EStoreReader.ReadMetaStreamSections(fileData);
        var defaultData = sections.defaultData;

        // Append the new 42-byte record
        var newDefault = new byte[defaultData.Length + EventLogEntry.RecordSize];
        Array.Copy(defaultData, newDefault, defaultData.Length);
        Array.Copy(record, 0, newDefault, defaultData.Length, EventLogEntry.RecordSize);

        // Rebuild the file with the extended default section
        // We need to use the same rebuild approach as EStoreWriter
        var rebuilt = RebuildMetaStreamWithDefault(fileData, newDefault);
        File.WriteAllBytes(targetPath, rebuilt);

        // Track the new entry
        var recordIndex = EStoreReader.ParseEventsFromSection(defaultData).Count;
        _cachedEntries?.Add(new TrackedEntry
        {
            Entry = newEntry,
            SourcePath = targetPath,
            RecordIndex = recordIndex,
        });
    }

    /// <summary>
    /// Rebuild a MetaStream file with a new default section.
    /// Same approach as EStoreWriter but accessible here for appending records.
    /// </summary>
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
        origReader.ReadBytes(origDefRawSize); // skip original default section

        var dbgRawSize = (int)(origDbgSize & 0x7FFFFFFF);
        var dbgData = dbgRawSize > 0 ? origReader.ReadBytes(dbgRawSize) : Array.Empty<byte>();

        var asyncRawSize = (int)(origAsyncSize & 0x7FFFFFFF);
        var asyncData = asyncRawSize > 0 ? origReader.ReadBytes(asyncRawSize) : Array.Empty<byte>();

        using var outMs = new MemoryStream();
        using var writer = new BinaryWriterEx(outMs, leaveOpen: true);

        writer.WriteUInt32(magic);
        writer.WriteUInt32((uint)newDefaultData.Length); // uncompressed
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

    private static int ExtractPageNumber(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var pageIdx = name.LastIndexOf("Page", StringComparison.Ordinal);
        if (pageIdx < 0) return 0;
        var numStr = name[(pageIdx + 4)..];
        return int.TryParse(numStr, out var num) ? num : 0;
    }
}
