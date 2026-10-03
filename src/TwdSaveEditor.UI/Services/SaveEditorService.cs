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
    private readonly Dictionary<SaveSlot, int> _revisions = [];
    private readonly Dictionary<string, byte[]> _uploaded = new(StringComparer.OrdinalIgnoreCase);
    private bool _directoryOpen;

    private const string BundleExtension = ".bundle";

    public List<SaveSlot> Saves { get; } = [];
    public SaveSlot? SelectedSave { get; set; }
    public string? DirectoryName { get; private set; }
    public string? RememberedDirectoryName { get; private set; }
    public bool DownloadsChanges => !_directoryOpen;
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

    public int Revision(SaveSlot slot) => _revisions.GetValueOrDefault(slot);

    public void MarkModified(SaveSlot slot)
    {
        RecordChange(slot);
        NotifyStateChanged();
    }

    private bool RecordChange(SaveSlot slot)
    {
        _revisions[slot] = Revision(slot) + 1;
        return _modified.Add(slot);
    }

    public async Task<bool> PickDirectory()
    {
        var result = await _fs.PickDirectory();
        if (result)
        {
            await OpenedDirectory();
        }

        return result;
    }

    public async Task<bool> ReopenDirectory()
    {
        var result = await _fs.ReopenDirectory();
        if (result)
        {
            await OpenedDirectory();
        }

        return result;
    }

    public async Task RestoreDirectory()
    {
        if (await _fs.RememberedDirectory() is not { } remembered)
        {
            return;
        }

        if (!remembered.Granted)
        {
            RememberedDirectoryName = remembered.Name;
            NotifyStateChanged();
            return;
        }

        await OpenedDirectory();
        await LoadDirectory();
    }

    public async Task ReloadDirectory()
    {
        var selected = SelectedSave?.FileName;
        await LoadDirectory();
        SelectedSave = Saves.FirstOrDefault(save => save.FileName.Equals(selected, StringComparison.OrdinalIgnoreCase));
        NotifyStateChanged();
    }

    private async Task OpenedDirectory()
    {
        DirectoryName = await _fs.GetDirectoryName();
        RememberedDirectoryName = null;
        _directoryOpen = true;
    }

    public async Task LoadDirectory()
    {
        IsLoading = true;
        StatusMessage = "Loading saves...";
        NotifyStateChanged();

        try
        {
            var loadedCount = await LoadSaves(await SourceFileNames());

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
        _directoryOpen = false;
        _uploaded.Clear();
        foreach (var (name, data) in files)
        {
            _uploaded[name] = data;
        }

        var loadedCount = await LoadSaves(await SourceFileNames());
        StatusMessage = $"Loaded {loadedCount} save(s).";
        NotifyStateChanged();
    }

    private async Task<int> LoadSaves(string[] fileNames)
    {
        Saves.Clear();
        _modified.Clear();
        _revisions.Clear();
        SelectedSave = null;

        var bundleFiles = fileNames
            .Where(name => name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var loadedCount = 0;
        foreach (var fileName in bundleFiles.Where(IsSaveFile))
        {
            try
            {
                StatusMessage = $"Loading {fileName}...";
                NotifyStateChanged();

                if (await ReadSave(fileName, fileNames) is not { } slot)
                {
                    continue;
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

    private bool IsSaveFile(string fileName) => _registry.DetectFromFileName(fileName) switch
    {
        null => false,
        ICompanionFileHandler companion => !companion.IsCompanionFile(fileName),
        _ => true,
    };

    public SaveSlot ReadBundle(byte[] data, string fileName)
    {
        var slot = _serializer.Read(data, fileName);
        slot.DetectedSeasonKey = _registry.DetectFromFileName(slot.FileName)?.SeasonKey;
        return slot;
    }

    private async Task<SaveSlot?> ReadSave(string fileName, string[] fileNames)
    {
        var data = await ReadSourceFile(fileName);
        if (data == null)
        {
            return null;
        }

        var slot = ReadBundle(data, fileName);
        if (_registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
        {
            await LoadCompanionFiles(companion, slot, fileNames);
        }

        return slot;
    }

    private async Task<string[]> SourceFileNames() => _directoryOpen ? await _fs.ListFiles() : [.. _uploaded.Keys];

    private async Task<byte[]?> ReadSourceFile(string name) => _directoryOpen ? await _fs.ReadFile(name) : _uploaded.GetValueOrDefault(name);

    private async Task LoadCompanionFiles(ICompanionFileHandler companion, SaveSlot slot, string[] fileNames)
    {
        var files = new List<CompanionFile>();
        foreach (var name in companion.FindCompanionFiles(slot.FileName, fileNames))
        {
            var data = await ReadSourceFile(name);
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
        if (_registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
        {
            files.AddRange(companion.BuildCompanionFiles(slot));
        }

        return files;
    }

    public async Task SaveFile(SaveSlot slot)
    {
        StatusMessage = $"Saving {slot.FileName}...";
        NotifyStateChanged();

        try
        {
            if (!_directoryOpen)
            {
                StatusMessage = await DownloadFiles(slot, BuildFiles(slot));
                _modified.Remove(slot);
                Notify(StatusMessage, "success");
                NotifyStateChanged();
                return;
            }

            await BackupBeforeSave(slot);

            if (!await WriteFiles(slot))
            {
                NotifyStateChanged();
                return;
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

    public async Task DiscardChanges(SaveSlot slot)
    {
        var index = Saves.IndexOf(slot);
        if (index < 0)
        {
            return;
        }

        try
        {
            if (await ReadSave(slot.FileName, await SourceFileNames()) is not { } reloaded)
            {
                Notify($"{slot.FileName} is no longer in the save folder.", "error");
                return;
            }

            Saves[index] = reloaded;
            _modified.Remove(slot);
            _revisions.Remove(slot);
            if (SelectedSave == slot)
            {
                SelectedSave = reloaded;
            }

            StatusMessage = $"Discarded the changes to {slot.FileName}.";
            Notify(StatusMessage, "info");
        }
        catch (Exception ex)
        {
            Notify($"Failed to reload {slot.FileName}: {ex.Message}", "error");
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    private async Task<string> DownloadFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files)
    {
        var archive = SaveArchive.Name(slot.FileName);
        await _fs.DownloadFile(archive, SaveArchive.Create(files, slot.ObsoleteFileNames));
        foreach (var file in files)
        {
            _uploaded[file.Name] = file.Data;
        }

        foreach (var name in slot.ObsoleteFileNames)
        {
            _uploaded.Remove(name);
        }

        var delete = slot.ObsoleteFileNames.Count > 0 ? $" and delete {string.Join(", ", slot.ObsoleteFileNames)}" : string.Empty;
        slot.ObsoleteFileNames.Clear();
        return $"Downloaded {archive}. Copy its files into your save folder{delete}.";
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

            var files = BuildFiles(slot);
            var downloaded = string.Empty;
            if (_directoryOpen)
            {
                await BackupBeforeSave(slot);
                if (!await WriteFiles(slot))
                {
                    NotifyStateChanged();
                    return null;
                }
            }
            else
            {
                downloaded = " " + await DownloadFiles(slot, files);
            }

            if (_registry.Get(seasonKey) is ICompanionFileHandler companion)
            {
                companion.AttachCompanionFiles(slot, files.Skip(1).ToList());
            }

            foreach (var replaced in Saves.Where(save => save.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                Saves.Remove(replaced);
                _modified.Remove(replaced);
                _revisions.Remove(replaced);
            }

            Saves.Add(slot);
            SelectedSave = slot;
            StatusMessage = $"Created {fileName}.{downloaded}";
            Notify($"Created {fileName} successfully.{downloaded}", "success");
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
                changed |= RecordChange(save);
            }
        }

        if (changed)
        {
            NotifyStateChanged();
        }
    }
}
