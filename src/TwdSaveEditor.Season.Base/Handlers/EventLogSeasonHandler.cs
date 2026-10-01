using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Base.EventLog;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Handlers;

public abstract class EventLogSeasonHandler : SeasonHandlerBase, ICompanionFileHandler
{
    protected abstract ChoiceNodeMap ChoiceNodes { get; }

    public override SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankMetadataOnly(fileName, episodeId);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => new EventLogAccessor(slot, ChoiceNodes);

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
        var eventEntries = new List<EventLogEntry>();
        uint seqIdx = 0;

        foreach (var c in GetChoicesUpTo(episode))
        {
            var nodeHash = ChoiceNodes.GetNodeHash(c.ChoiceKey, c.Options[0].Value);
            if (nodeHash == null)
                continue;

            eventEntries.Add(new EventLogEntry
            {
                EventTypeHash = EventLogEventTypes.ExecutingDialogNode,
                NodeHash = nodeHash.Value,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = seqIdx++,
                Trailing = 0,
            });
        }

        slot.PendingEventLogEntries = eventEntries;
    }

    public bool IsCompanionFile(string fileName) =>
        fileName.EndsWith(EventLogFiles.EStoreExtension, StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(EventLogFiles.EPageExtension, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        var names = directoryFileNames as IReadOnlyCollection<string> ?? directoryFileNames.ToList();

        var estoreName = EventLogFiles.GetEStoreName(bundleFileName);
        var estore = names.FirstOrDefault(f => f.Equals(estoreName, StringComparison.OrdinalIgnoreCase));
        if (estore == null)
            return [];

        var pagePrefix = EventLogFiles.GetEPagePrefix(bundleFileName);
        var epages = names
            .Where(f => f.StartsWith(pagePrefix, StringComparison.OrdinalIgnoreCase) &&
                        f.EndsWith(EventLogFiles.EPageExtension, StringComparison.Ordinal))
            .OrderBy(EventLogFiles.ExtractPageNumber);

        return [estore, .. epages];
    }

    public void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files)
    {
        var estore = files.FirstOrDefault(f =>
            f.Name.EndsWith(EventLogFiles.EStoreExtension, StringComparison.OrdinalIgnoreCase));
        if (estore == null)
            return;

        var epages = files
            .Where(f => !ReferenceEquals(f, estore))
            .OrderBy(f => EventLogFiles.ExtractPageNumber(f.Name))
            .ToList();

        var entries = new List<EventLogEntry>();
        var loadedFiles = new Dictionary<string, byte[]>();

        foreach (var file in epages.Prepend(estore))
        {
            loadedFiles[file.Name] = file.Data;

            try
            {
                var sections = EStoreReader.ReadMetaStreamSections(file.Data);
                if (sections.defaultData.Length > 0)
                    entries.AddRange(EStoreReader.ParseEventsFromSection(sections.defaultData));
            }
            catch { }
        }

        slot.LoadedEventLogEntries = entries;
        slot.LoadedEventLogFiles = loadedFiles;
        slot.EStorePath = estore.Name;
        slot.EPagePaths = epages.Select(f => f.Name).ToList();
    }

    public IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot)
    {
        var events = slot.LoadedEventLogEntries ?? slot.PendingEventLogEntries;
        if (events == null)
            return [];

        var slotBaseName = EventLogFiles.GetSlotBaseName(slot.FileName);
        var (estoreBytes, epageBytes, epageFilename) = EStoreCreator.Create(slotBaseName, events);

        return
        [
            new CompanionFile(EventLogFiles.GetEStoreName(slot.FileName), estoreBytes),
            new CompanionFile(epageFilename, epageBytes),
        ];
    }

    public IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot)
    {
        if (slot.EStorePath is null)
            return [];

        return [slot.EStorePath, .. slot.EPagePaths ?? []];
    }
}
