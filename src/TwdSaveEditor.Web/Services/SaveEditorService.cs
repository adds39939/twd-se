using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Services;

public class SaveEditorService
{
    private readonly FileSystemService _fs;
    private readonly ISeasonRegistry _registry;
    private readonly SaveBackupService _backup;

    public List<SaveSlot> Saves { get; } = [];
    public SaveSlot? SelectedSave { get; set; }
    public string? DirectoryName { get; private set; }
    public string StatusMessage { get; set; } = "Select a save directory to begin.";
    public bool IsLoading { get; set; }

    public bool HasUnsavedChanges { get; private set; }
    public bool CascadeChoices { get; set; }

    public event Action? StateChanged;
    public event Action<string, string>? OnNotification;

    public SaveEditorService(FileSystemService fs, ISeasonRegistry registry, SaveBackupService backup)
    {
        _fs = fs;
        _registry = registry;
        _backup = backup;
    }

    public void NotifyStateChanged() => StateChanged?.Invoke();

    private void Notify(string message, string type = "info")
    {
        OnNotification?.Invoke(message, type);
    }

    public void MarkModified()
    {
        HasUnsavedChanges = true;
        NotifyStateChanged();
    }

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
                    Notify($"Failed to load {fileName}: {ex.Message}", "error");
                }
            }

            StatusMessage = $"Loaded {loadedCount} save(s) from {DirectoryName}.";
            if (loadedCount == 0)
            {
                StatusMessage = "No valid save files found in the selected directory.";
                Notify("No valid save files found in the selected directory.", "info");
            }
            else
            {
                Notify($"Loaded {loadedCount} save(s) from {DirectoryName}.", "success");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Notify($"Error loading directory: {ex.Message}", "error");
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
        var fileName = Path.GetFileName(slot.FileName);
        var isAutosave = fileName.StartsWith('_');

        StatusMessage = $"Saving {slot.FileName}...";
        NotifyStateChanged();

        try
        {
            // Backup original files before modifying
            await BackupBeforeSave(slot);

            byte[] fileBytes;

            if (isAutosave)
            {
                // Autosave/checkpoint bundles have complex file tables that can't be
                // safely round-tripped. Use MetadataPatcher to patch only the metadata
                // PropertySet in-place, preserving everything else byte-for-byte.
                if (slot.RawBundleData == null || slot.Metadata == null || slot.RawMetadataFile == null)
                {
                    Notify("Cannot save autosave — missing raw bundle data.", "error");
                    NotifyStateChanged();
                    return;
                }
                fileBytes = MetadataPatcher.PatchMetadata(
                    slot.RawBundleData, slot.Metadata, slot.RawMetadataFile);
            }
            else
            {
                fileBytes = BundleWriter.Write(slot);
            }

            var success = await _fs.WriteFile(slot.FileName, fileBytes);

            if (!success)
            {
                StatusMessage = $"Failed to write {slot.FileName}.";
                Notify($"Failed to write {slot.FileName}.", "error");
                NotifyStateChanged();
                return;
            }

            // Write estore/epage for S3/Michonne if we have loaded entries
            if (!isAutosave)
            {
                var saveHandler = _registry.DetectFromFileName(slot.FileName);
                if (saveHandler?.UsesEventLog == true && slot.LoadedEventLogEntries != null)
                {
                    await WriteEventLogFiles(slot);
                }
            }

            // When saving an autosave, also update the slot bundle's episode ID
            // so the game's save/load menu shows the correct episode
            if (isAutosave)
            {
                await SyncSlotBundleEpisodeId(slot);
            }

            StatusMessage = $"Saved {slot.FileName} successfully.";
            HasUnsavedChanges = false;
            Notify($"Saved {slot.FileName} successfully.", "success");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
            Notify($"Error saving: {ex.Message}", "error");
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// When saving an autosave, sync the episode ID back to the corresponding slot bundle.
    /// The game menu reads the episode from the slot's metadata_slot.p, not the autosave.
    /// e.g. _wd1_saveslot1_autosave.bundle → wd1_saveslot1.bundle
    /// </summary>
    private async Task SyncSlotBundleEpisodeId(SaveSlot autosaveSlot)
    {
        // Get the episode ID from the autosave metadata
        var episodeIdProp = autosaveSlot.Metadata?.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == ResumePoint.AutosaveHashes.EpisodeId);
        if (episodeIdProp?.Value is not StringValue episodeIdValue)
        {
            Notify("Sync: no episode ID found in autosave metadata.", "warning");
            return;
        }

        // Derive slot bundle name: _wd1_saveslot1_autosave.bundle → wd1_saveslot1.bundle
        var autoName = Path.GetFileNameWithoutExtension(autosaveSlot.FileName);
        if (!autoName.StartsWith('_') || !autoName.EndsWith("_autosave"))
        {
            Notify($"Sync: autosave name '{autoName}' doesn't match expected pattern.", "warning");
            return;
        }
        var slotName = autoName[1..^9] + ".bundle"; // strip leading _ and trailing _autosave
        // Find the slot in loaded saves

        // Find the slot in loaded saves
        var slotSave = Saves.FirstOrDefault(s =>
            Path.GetFileName(s.FileName).Equals(slotName, StringComparison.OrdinalIgnoreCase));

        if (slotSave == null)
        {
            Notify($"Could not find slot bundle '{slotName}' to sync episode. Load both files.", "warning");
            return;
        }
        if (slotSave.Metadata == null) return;

        // Update or create the episode ID property (hash 0xB218E7C003A67CE9)
        const ulong slotEpisodeIdHash = 0xB218E7C003A67CE9;
        var slotEpProp = slotSave.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == slotEpisodeIdHash);

        // Update "Episode in Progress" (String)
        // Update "Episode in Progress" (String)
        SetSlotProperty(slotSave.Metadata, slotEpisodeIdHash,
            new StringValue(episodeIdValue.Value), "String");

        // Determine episode number from episode ID (e.g. "WalkingDead102" → 2)
        var epNumStr = episodeIdValue.Value.Length >= 3
            ? episodeIdValue.Value[^2..] // last 2 chars
            : "01";
        if (int.TryParse(epNumStr, out var epNum))
        {
            // "progress" = current episode number (int) — this is what the save menu reads
            const ulong progressHash = 0x94C245DACB1ADDC3;
            SetSlotProperty(slotSave.Metadata, progressHash,
                new IntValue(epNum), "int32");

            if (epNum > 1)
            {
                // "Last Episode Finished" = previous episode number (int)
                const ulong lastEpFinishedHash = 0x0399C2FFE0D50348;
                SetSlotProperty(slotSave.Metadata, lastEpFinishedHash,
                    new IntValue(epNum - 1), "int32");

                // "Episodes Completed" = number of episodes completed (int)
                const ulong episodesCompletedHash = 0xFD50E3BE7B29A8B1;
                SetSlotProperty(slotSave.Metadata, episodesCompletedHash,
                    new IntValue(epNum - 1), "int32");
            }
        }

        // Write the updated slot bundle
        try
        {
            var slotBytes = BundleWriter.Write(slotSave);
            var wrote = await _fs.WriteFile(slotSave.FileName, slotBytes);
            if (wrote)
                Notify($"Updated slot bundle episode to {episodeIdValue.Value}.", "info");
            else
                Notify($"Failed to write slot bundle '{slotName}'.", "warning");
        }
        catch (Exception ex)
        {
            Notify($"Warning: could not sync episode to slot bundle: {ex.Message}", "warning");
        }
    }

    private static void SetSlotProperty(PropertySet metadata, ulong hash, PropertyValue value, string typeName)
    {
        var existing = metadata.AllProperties.FirstOrDefault(p => p.KeySymbol.Value == hash);
        if (existing != null)
        {
            // Update in-place: find the property in its type group and replace value
            foreach (var group in metadata.TypeGroups)
            {
                var prop = group.Properties.FirstOrDefault(p => p.KeySymbol.Value == hash);
                if (prop != null)
                {
                    if (value is StringValue sv && prop.Value is StringValue existingSv)
                        existingSv.Value = sv.Value;
                    else if (value is IntValue iv && prop.Value is IntValue existingIv)
                        existingIv.Value = iv.Value;
                    else if (value is BoolValue bv && prop.Value is BoolValue existingBv)
                        existingBv.Value = bv.Value;
                    return;
                }
            }
        }

        // Property doesn't exist — add it
        var typeSymbol = Symbol.FromString(typeName);
        var targetGroup = metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == typeSymbol);
        if (targetGroup == null)
        {
            targetGroup = new TypeGroup(typeSymbol);
            metadata.TypeGroups.Add(targetGroup);
        }
        targetGroup.Properties.Add(new Property(new Symbol(hash), value));
    }

    private async Task BackupBeforeSave(SaveSlot slot)
    {
        var backupFolder = await _backup.BackupBeforeSave(slot);
        if (backupFolder != null)
            Notify($"Backup created in {backupFolder}/", "info");
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
                Notify($"Failed to create {fileName}.", "error");
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
            Notify($"Created {fileName} successfully.", "success");
            NotifyStateChanged();
            return slot;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating save: {ex.Message}";
            Notify($"Error creating save: {ex.Message}", "error");
            NotifyStateChanged();
            return null;
        }
    }

    public IChoiceAccessor? GetChoiceAccessor(SaveSlot slot)
    {
        return _registry.DetectFromFileName(slot.FileName)?.CreateChoiceAccessor(slot);
    }

    /// <summary>
    /// Propagate a choice change from the current save to later season saves.
    /// S1 choices cascade to S2 (season1.prop). S1/S2 choices cascade to S3/S4 EventLog where applicable.
    /// </summary>
    public void CascadeChoice(string choiceKey, string value, string sourceSeasonKey)
    {
        if (!CascadeChoices) return;

        // Cascade chain based on game's SaveFileImporter.lua:
        // S1 → S2 (via season1.prop in bundle)
        // S2 → S3 (via estore/epage EventLog)
        // S3 → S4 (via estore/epage EventLog)
        // Michonne is standalone (no imports)
        var targetSeasons = sourceSeasonKey switch
        {
            "s1" or "s1_400days" => new[] { "s2" },
            "s2" => new[] { "s3" },
            "s3" => new[] { "s4" },
            _ => Array.Empty<string>(),
        };

        foreach (var targetSeason in targetSeasons)
        {
            foreach (var save in Saves)
            {
                if (save.DetectedSeasonKey != targetSeason) continue;

                var accessor = GetChoiceAccessor(save);
                if (accessor == null) continue;

                try
                {
                    accessor.SetChoiceValue(choiceKey, value);
                }
                catch (InvalidOperationException)
                {
                    // Target save doesn't support this choice format — skip
                }
            }
        }
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
