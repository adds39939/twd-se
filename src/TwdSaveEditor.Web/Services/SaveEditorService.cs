using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Services;

public class SaveEditorService
{
    private readonly FileSystemService _fs;
    private readonly ISeasonRegistry _registry;

    public List<SaveSlot> Saves { get; } = [];
    public SaveSlot? SelectedSave { get; set; }
    public string? DirectoryName { get; private set; }
    public string StatusMessage { get; set; } = "Select a save directory to begin.";
    public bool IsLoading { get; set; }

    public event Action? StateChanged;

    public SaveEditorService(FileSystemService fs, ISeasonRegistry registry)
    {
        _fs = fs;
        _registry = registry;
    }

    public void NotifyStateChanged() => StateChanged?.Invoke();

    public async Task<bool> PickDirectory()
    {
        var result = await _fs.PickDirectory();
        if (result)
        {
            DirectoryName = await _fs.GetDirectoryName();
        }
        return result;
    }

    public async Task LoadDirectory()
    {
        IsLoading = true;
        StatusMessage = "Loading saves...";
        NotifyStateChanged();

        try
        {
            Saves.Clear();
            SelectedSave = null;

            // Load .bundle files
            var bundleFiles = await _fs.ListFiles(".bundle");
            Array.Sort(bundleFiles, StringComparer.OrdinalIgnoreCase);

            // Also discover estore/epage files for S3/Michonne
            var estoreFiles = await _fs.ListFiles(".estore");
            var epageFiles = await _fs.ListFiles(".epage");

            var loadedCount = 0;
            foreach (var fileName in bundleFiles)
            {
                try
                {
                    StatusMessage = $"Loading {fileName}...";
                    NotifyStateChanged();

                    var data = await _fs.ReadFile(fileName);
                    if (data == null) continue;

                    var slot = BundleReader.Read(data, fileName);
                    var slotHandler = _registry.DetectFromFileName(slot.FileName);
                    slot.DetectedSeasonKey = slotHandler?.SeasonKey;
                    if (slotHandler?.UsesEventLog == true)
                    {
                        await LoadEventLogFiles(slot, fileName, estoreFiles, epageFiles);
                    }

                    Saves.Add(slot);
                    loadedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load {fileName}: {ex.Message}");
                }
            }

            StatusMessage = $"Loaded {loadedCount} save(s) from {DirectoryName}.";
            if (loadedCount == 0)
                StatusMessage = "No valid save files found in the selected directory.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private async Task LoadEventLogFiles(SaveSlot slot, string bundleFileName,
        string[] estoreFiles, string[] epageFiles)
    {
        var bundleName = Path.GetFileNameWithoutExtension(bundleFileName);
        var estoreName = $"_{bundleName}_id.estore";

        // Find matching estore
        var matchingEstore = estoreFiles.FirstOrDefault(f =>
            f.Equals(estoreName, StringComparison.OrdinalIgnoreCase));

        if (matchingEstore == null) return;

        // Read estore file
        var estoreData = await _fs.ReadFile(matchingEstore);
        if (estoreData == null) return;

        var entries = new List<EventLogEntry>();

        // Parse estore entries
        try
        {
            var sections = EStoreReader.ReadMetaStreamSections(estoreData);
            if (sections.defaultData.Length > 0)
                entries.AddRange(EStoreReader.ParseEventsFromSection(sections.defaultData));
        }
        catch { /* skip on parse error */ }

        // Find and read epage files
        var pagePrefix = $"_{bundleName}_id_Page";
        var matchingEpages = epageFiles
            .Where(f => f.StartsWith(pagePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => ExtractPageNumber(f))
            .ToList();

        var loadedFiles = new Dictionary<string, byte[]>
        {
            [matchingEstore] = estoreData
        };

        foreach (var epageName in matchingEpages)
        {
            try
            {
                var epageData = await _fs.ReadFile(epageName);
                if (epageData == null) continue;

                loadedFiles[epageName] = epageData;

                var sections = EStoreReader.ReadMetaStreamSections(epageData);
                if (sections.defaultData.Length > 0)
                    entries.AddRange(EStoreReader.ParseEventsFromSection(sections.defaultData));
            }
            catch { /* skip on parse error */ }
        }

        slot.LoadedEventLogEntries = entries;
        slot.LoadedEventLogFiles = loadedFiles;
        slot.EStorePath = matchingEstore;
        slot.EPagePaths = matchingEpages;
    }

    public async Task SaveFile(SaveSlot slot)
    {
        StatusMessage = $"Saving {slot.FileName}...";
        NotifyStateChanged();

        try
        {
            var fileBytes = BundleWriter.Write(slot);
            var success = await _fs.WriteFile(slot.FileName, fileBytes);

            if (!success)
            {
                StatusMessage = $"Failed to write {slot.FileName}.";
                NotifyStateChanged();
                return;
            }

            // Write estore/epage for S3/Michonne if we have loaded entries
            var saveHandler = _registry.DetectFromFileName(slot.FileName);
            if (saveHandler?.UsesEventLog == true && slot.LoadedEventLogEntries != null)
            {
                await WriteEventLogFiles(slot);
            }

            StatusMessage = $"Saved {slot.FileName} successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }

        NotifyStateChanged();
    }

    private async Task WriteEventLogFiles(SaveSlot slot)
    {
        var bundleName = Path.GetFileNameWithoutExtension(slot.FileName);
        var slotBaseName = $"_{bundleName}";
        var events = slot.LoadedEventLogEntries ?? [];

        var (estoreBytes, epageBytes, epageFilename) =
            EStoreCreator.Create(slotBaseName, events);

        var estoreName = $"{slotBaseName}_id.estore";
        await _fs.WriteFile(estoreName, estoreBytes);
        await _fs.WriteFile(epageFilename, epageBytes);
    }

    public async Task<SaveSlot?> CreateNewSave(string seasonKey, int episode, string fileName)
    {
        StatusMessage = $"Creating {fileName}...";
        NotifyStateChanged();

        try
        {
            var slot = SaveSlotFactory.CreateForSeason(_registry, seasonKey, episode, fileName);
            slot.DetectedSeasonKey = seasonKey;

            // Write the bundle file
            var fileBytes = BundleWriter.Write(slot);
            var success = await _fs.WriteFile(fileName, fileBytes);

            if (!success)
            {
                StatusMessage = $"Failed to create {fileName}.";
                NotifyStateChanged();
                return null;
            }

            // S3/Michonne: create and write estore/epage files
            var newHandler = _registry.Get(seasonKey);
            if (newHandler?.UsesEventLog == true)
            {
                var bundleName = Path.GetFileNameWithoutExtension(fileName);
                var slotBaseName = $"_{bundleName}";
                var events = slot.PendingEventLogEntries ?? [];

                var (estoreBytes, epageBytes, epageFilename) =
                    EStoreCreator.Create(slotBaseName, events);

                var estoreName = $"{slotBaseName}_id.estore";
                await _fs.WriteFile(estoreName, estoreBytes);
                await _fs.WriteFile(epageFilename, epageBytes);

                // Set up the slot for in-memory event log access
                slot.LoadedEventLogEntries = events;
                slot.EStorePath = estoreName;
                slot.EPagePaths = [epageFilename];
            }

            Saves.Add(slot);
            SelectedSave = slot;
            StatusMessage = $"Created {fileName}.";
            NotifyStateChanged();
            return slot;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating save: {ex.Message}";
            NotifyStateChanged();
            return null;
        }
    }

    public IChoiceAccessor? GetChoiceAccessor(SaveSlot slot)
    {
        return _registry.DetectFromFileName(slot.FileName)?.CreateChoiceAccessor(slot);
    }

    private static int ExtractPageNumber(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var pageIdx = name.LastIndexOf("Page", StringComparison.Ordinal);
        if (pageIdx < 0) return 0;
        var numStr = name[(pageIdx + 4)..];
        return int.TryParse(numStr, out var num) ? num : 0;
    }
}
