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
    private readonly HashSet<SaveSlot> _modified = [];

    private const string BundleExtension = ".bundle";

    public List<SaveSlot> Saves { get; } = [];
    public SaveSlot? SelectedSave { get; set; }
    public string? DirectoryName { get; private set; }
    public string StatusMessage { get; set; } = "Select a save directory to begin.";
    public bool IsLoading { get; set; }

    public bool HasUnsavedChanges => _modified.Count > 0;
    public IReadOnlyList<SaveSlot> ModifiedSaves => [.. Saves.Where(_modified.Contains)];
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

    public void ShowError(string message) => Notify(message, "error");

    public bool IsModified(SaveSlot slot) => _modified.Contains(slot);

    public void MarkModified(SaveSlot slot)
    {
        _modified.Add(slot);
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
            var directoryFiles = await _fs.ListFiles(string.Empty);
            var loadedCount = await LoadSaves(directoryFiles, _fs.ReadFile);

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

    public async Task LoadFiles(IReadOnlyDictionary<string, byte[]> files)
    {
        var loadedCount = await LoadSaves(files.Keys.ToArray(), name => Task.FromResult(files.GetValueOrDefault(name)));
        StatusMessage = $"Loaded {loadedCount} save(s).";
        NotifyStateChanged();
    }

    private async Task<int> LoadSaves(string[] fileNames, Func<string, Task<byte[]?>> readFile)
    {
        Saves.Clear();
        _modified.Clear();
        SelectedSave = null;

        var bundleFiles = fileNames
            .Where(name => name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var loadedCount = 0;
        foreach (var fileName in bundleFiles.Where(name => !IsCompanionFile(name)))
        {
            try
            {
                StatusMessage = $"Loading {fileName}...";
                NotifyStateChanged();

                var data = await readFile(fileName);
                if (data == null)
                {
                    continue;
                }

                var slot = ReadBundle(data, fileName);
                if (_registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
                {
                    await LoadCompanionFiles(companion, slot, fileNames, readFile);
                }

                Saves.Add(slot);
                loadedCount++;
            }
            catch (Exception ex)
            {
                Notify($"Failed to load {fileName}: {ex.Message}", "error");
            }
        }

        return loadedCount;
    }

    private bool IsCompanionFile(string fileName) =>
        _registry.DetectFromFileName(fileName) is ICompanionFileHandler companion && companion.IsCompanionFile(fileName);

    public SaveSlot ReadBundle(byte[] data, string fileName)
    {
        var slot = _serializer.Read(data, fileName);
        slot.DetectedSeasonKey = _registry.DetectFromFileName(slot.FileName)?.SeasonKey;
        return slot;
    }

    private static async Task LoadCompanionFiles(ICompanionFileHandler companion, SaveSlot slot, string[] fileNames,
        Func<string, Task<byte[]?>> readFile)
    {
        var files = new List<CompanionFile>();
        foreach (var name in companion.FindCompanionFiles(slot.FileName, fileNames))
        {
            var data = await readFile(name);
            if (data != null)
            {
                files.Add(new CompanionFile(name, data));
            }
        }

        companion.AttachCompanionFiles(slot, files);
    }

    public IReadOnlyList<CompanionFile> BuildFiles(SaveSlot slot)
    {
        var files = new List<CompanionFile> { new(slot.FileName, _serializer.Write(slot)) };
        if (!IsAutosave(slot) && _registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
        {
            files.AddRange(companion.BuildCompanionFiles(slot));
        }

        return files;
    }

    private static bool IsAutosave(SaveSlot slot) => Path.GetFileName(slot.FileName).StartsWith('_');

    public async Task SaveFile(SaveSlot slot)
    {
        StatusMessage = $"Saving {slot.FileName}...";
        NotifyStateChanged();

        try
        {
            await BackupBeforeSave(slot);

            if (!await WriteFiles(slot))
            {
                NotifyStateChanged();
                return;
            }

            if (IsAutosave(slot))
            {
                await SyncSlotBundleEpisodeId(slot);
            }

            StatusMessage = $"Saved {slot.FileName} successfully.";
            _modified.Remove(slot);
            Notify($"Saved {slot.FileName} successfully.", "success");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
            Notify($"Error saving: {ex.Message}", "error");
        }

        NotifyStateChanged();
    }

    private async Task<bool> WriteFiles(SaveSlot slot)
    {
        foreach (var file in BuildFiles(slot))
        {
            if (await _fs.WriteFile(file.Name, file.Data))
            {
                continue;
            }

            StatusMessage = $"Failed to write {file.Name}.";
            Notify($"Failed to write {file.Name}.", "error");
            return false;
        }

        foreach (var name in slot.ObsoleteFileNames)
        {
            if (await _fs.DeleteFile(name))
            {
                Notify($"Removed {name}.", "info");
            }
        }

        slot.ObsoleteFileNames.Clear();
        return true;
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
        if (slotSave.Metadata == null)
        {
            return;
        }

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
            {
                Notify($"Updated slot bundle episode to {episodeIdValue.Value}.", "info");
            }
            else
            {
                Notify($"Failed to write slot bundle '{slotName}'.", "warning");
            }
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
                    {
                        existingSv.Value = sv.Value;
                    }
                    else if (value is IntValue iv && prop.Value is IntValue existingIv)
                    {
                        existingIv.Value = iv.Value;
                    }
                    else if (value is BoolValue bv && prop.Value is BoolValue existingBv)
                    {
                        existingBv.Value = bv.Value;
                    }

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
        {
            Notify($"Backup created in {backupFolder}/", "info");
        }
    }

    public async Task<SaveSlot?> CreateNewSave(string seasonKey, int episode, string fileName)
    {
        StatusMessage = $"Creating {fileName}...";
        NotifyStateChanged();

        try
        {
            var slot = _registry.CreateSave(seasonKey, episode, fileName);
            slot.DetectedSeasonKey = _registry.DetectFromFileName(fileName)?.SeasonKey ?? seasonKey;

            await BackupBeforeSave(slot);

            var files = BuildFiles(slot);
            if (!await WriteFiles(slot))
            {
                NotifyStateChanged();
                return null;
            }

            if (_registry.Get(seasonKey) is ICompanionFileHandler companion)
            {
                companion.AttachCompanionFiles(slot, files.Skip(1).ToList());
            }

            _modified.RemoveWhere(save => save.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            Saves.RemoveAll(save => save.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
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

    public void CascadeChoice(string choiceKey, string? value, string sourceSeasonKey)
    {
        if (!CascadeChoices)
        {
            return;
        }

        var changed = false;
        foreach (var save in Saves)
        {
            if (save.DetectedSeasonKey == null)
            {
                continue;
            }

            var targetSeason = _registry.Get(save.DetectedSeasonKey);
            if (targetSeason?.ImportsFromSeasonKeys.Contains(sourceSeasonKey) != true)
            {
                continue;
            }

            var accessor = GetChoiceAccessor(save);
            if (accessor == null)
            {
                continue;
            }

            var before = accessor.GetChoiceValue(choiceKey);
            try
            {
                if (value == null)
                {
                    accessor.ClearChoiceValue(choiceKey);
                }
                else
                {
                    accessor.SetChoiceValue(choiceKey, value);
                }
            }
            catch (InvalidOperationException)
            {
            }

            if (accessor.GetChoiceValue(choiceKey) != before)
            {
                changed |= _modified.Add(save);
            }
        }

        if (changed)
        {
            NotifyStateChanged();
        }
    }
}
