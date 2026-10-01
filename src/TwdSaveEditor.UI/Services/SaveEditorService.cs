using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.UI.Services;

public class SaveEditorService
{
    private readonly IFileSystemService _fs;
    private readonly ISeasonRegistry _registry;
    private readonly ISaveBundleSerializer _serializer;
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

    public SaveEditorService(IFileSystemService fs, ISeasonRegistry registry,
        ISaveBundleSerializer serializer, SaveBackupService backup)
    {
        _fs = fs;
        _registry = registry;
        _serializer = serializer;
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

            var bundleFiles = await _fs.ListFiles(".bundle");
            Array.Sort(bundleFiles, StringComparer.OrdinalIgnoreCase);

            var directoryFiles = await _fs.ListFiles(string.Empty);

            var loadedCount = 0;
            foreach (var fileName in bundleFiles)
            {
                try
                {
                    StatusMessage = $"Loading {fileName}...";
                    NotifyStateChanged();

                    var data = await _fs.ReadFile(fileName);
                    if (data == null) continue;

                    var slot = ReadBundle(data, fileName);
                    if (_registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
                    {
                        await LoadCompanionFiles(companion, slot, directoryFiles);
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

    public SaveSlot ReadBundle(byte[] data, string fileName)
    {
        var slot = _serializer.Read(data, fileName);
        slot.DetectedSeasonKey = _registry.DetectFromFileName(slot.FileName)?.SeasonKey;
        return slot;
    }

    public byte[] WriteBundle(SaveSlot slot) => _serializer.Write(slot);

    private async Task LoadCompanionFiles(ICompanionFileHandler companion, SaveSlot slot, string[] directoryFiles)
    {
        var files = new List<CompanionFile>();
        foreach (var name in companion.FindCompanionFiles(slot.FileName, directoryFiles))
        {
            var data = await _fs.ReadFile(name);
            if (data != null)
                files.Add(new CompanionFile(name, data));
        }

        companion.AttachCompanionFiles(slot, files);
    }

    public async Task SaveFile(SaveSlot slot)
    {
        var fileName = Path.GetFileName(slot.FileName);
        var isAutosave = fileName.StartsWith('_');

        StatusMessage = $"Saving {slot.FileName}...";
        NotifyStateChanged();

        try
        {
            await BackupBeforeSave(slot);

            byte[] fileBytes;

            if (isAutosave)
            {
                if (!_serializer.CanPatchMetadata(slot))
                {
                    Notify("Cannot save autosave — missing raw bundle data.", "error");
                    NotifyStateChanged();
                    return;
                }
                fileBytes = _serializer.PatchMetadata(slot);
            }
            else
            {
                fileBytes = _serializer.Write(slot);
            }

            var success = await _fs.WriteFile(slot.FileName, fileBytes);

            if (!success)
            {
                StatusMessage = $"Failed to write {slot.FileName}.";
                Notify($"Failed to write {slot.FileName}.", "error");
                NotifyStateChanged();
                return;
            }

            if (!isAutosave && _registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
            {
                await WriteCompanionFiles(companion, slot);
            }

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

    private async Task SyncSlotBundleEpisodeId(SaveSlot autosaveSlot)
    {
        var episodeIdProp = autosaveSlot.Metadata?.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);
        if (episodeIdProp?.Value is not StringValue episodeIdValue)
        {
            Notify("Sync: no episode ID found in autosave metadata.", "warning");
            return;
        }

        var autoName = Path.GetFileNameWithoutExtension(autosaveSlot.FileName);
        if (!autoName.StartsWith('_') || !autoName.EndsWith("_autosave"))
        {
            Notify($"Sync: autosave name '{autoName}' doesn't match expected pattern.", "warning");
            return;
        }
        var slotName = autoName[1..^9] + ".bundle";

        var slotSave = Saves.FirstOrDefault(s =>
            Path.GetFileName(s.FileName).Equals(slotName, StringComparison.OrdinalIgnoreCase));

        if (slotSave == null)
        {
            Notify($"Could not find slot bundle '{slotName}' to sync episode. Load both files.", "warning");
            return;
        }
        if (slotSave.Metadata == null) return;

        const ulong slotEpisodeIdHash = 0xB218E7C003A67CE9;
        var slotEpProp = slotSave.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == slotEpisodeIdHash);

        SetSlotProperty(slotSave.Metadata, slotEpisodeIdHash,
            new StringValue(episodeIdValue.Value), "String");

        var epNumStr = episodeIdValue.Value.Length >= 3
            ? episodeIdValue.Value[^2..]
            : "01";
        if (int.TryParse(epNumStr, out var epNum))
        {
            const ulong progressHash = 0x94C245DACB1ADDC3;
            SetSlotProperty(slotSave.Metadata, progressHash,
                new IntValue(epNum), "int32");

            if (epNum > 1)
            {
                const ulong lastEpFinishedHash = 0x0399C2FFE0D50348;
                SetSlotProperty(slotSave.Metadata, lastEpFinishedHash,
                    new IntValue(epNum - 1), "int32");

                const ulong episodesCompletedHash = 0xFD50E3BE7B29A8B1;
                SetSlotProperty(slotSave.Metadata, episodesCompletedHash,
                    new IntValue(epNum - 1), "int32");
            }
        }

        try
        {
            var slotBytes = _serializer.Write(slotSave);
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

    private async Task<IReadOnlyList<CompanionFile>> WriteCompanionFiles(ICompanionFileHandler companion, SaveSlot slot)
    {
        var files = companion.BuildCompanionFiles(slot);
        foreach (var file in files)
            await _fs.WriteFile(file.Name, file.Data);

        return files;
    }

    public async Task<SaveSlot?> CreateNewSave(string seasonKey, int episode, string fileName)
    {
        StatusMessage = $"Creating {fileName}...";
        NotifyStateChanged();

        try
        {
            var slot = _registry.CreateSave(seasonKey, episode, fileName);
            slot.DetectedSeasonKey = seasonKey;

            var fileBytes = _serializer.Write(slot);
            var success = await _fs.WriteFile(fileName, fileBytes);

            if (!success)
            {
                StatusMessage = $"Failed to create {fileName}.";
                Notify($"Failed to create {fileName}.", "error");
                NotifyStateChanged();
                return null;
            }

            if (_registry.Get(seasonKey) is ICompanionFileHandler companion)
            {
                var files = await WriteCompanionFiles(companion, slot);
                companion.AttachCompanionFiles(slot, files);
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

    public void CascadeChoice(string choiceKey, string value, string sourceSeasonKey)
    {
        if (!CascadeChoices) return;

        foreach (var save in Saves)
        {
            if (save.DetectedSeasonKey == null) continue;

            var targetSeason = _registry.Get(save.DetectedSeasonKey);
            if (targetSeason?.ImportsFromSeasonKeys.Contains(sourceSeasonKey) != true) continue;

            var accessor = GetChoiceAccessor(save);
            if (accessor == null) continue;

            try
            {
                accessor.SetChoiceValue(choiceKey, value);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
