using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwdSaveEditor.Core;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SaveManager _saveManager = new();
    private readonly PropertyNameDb _nameDb;

    public MainViewModel()
    {
        _nameDb = PropertyNameDb.CreateDefault();
        LoadEmbeddedNames();
    }

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private SaveSlotViewModel? _selectedSlot;

    [ObservableProperty]
    private PropertyViewModel? _selectedProperty;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _hasFiles;

    [ObservableProperty]
    private ResumePointViewModel? _resumePoint;

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<SaveSlotViewModel> SaveSlots { get; } = [];
    public ObservableCollection<SeasonViewModel> Seasons { get; } = [];

    public string SaveDirectory => _saveManager.SaveDirectory;

    partial void OnSelectedSlotChanged(SaveSlotViewModel? value)
    {
        RebuildDecisionAndResumeViews();
    }

    private void RebuildDecisionAndResumeViews()
    {
        Seasons.Clear();
        ResumePoint = null;

        if (SelectedSlot?.Slot.Choices == null)
            return;

        var accessor = new SaveAccessor(SelectedSlot.Slot.Choices, SelectedSlot.Slot.Metadata);

        // Build resume point editor
        ResumePoint = new ResumePointViewModel(accessor);

        // Build season/episode/choice tree
        foreach (var seasonDef in SeasonInfo.Seasons)
        {
            var seasonVm = new SeasonViewModel(seasonDef);
            var seasonChoices = ChoiceDatabase.ForSeason(seasonDef.Key).ToList();

            foreach (var ep in seasonDef.Episodes)
            {
                var epChoices = seasonChoices
                    .Where(c => c.Episode == ep.Number)
                    .Select(c => new ChoiceViewModel(c, accessor))
                    .ToList();

                var epVm = new EpisodeViewModel(ep, epChoices);
                seasonVm.Episodes.Add(epVm);
            }

            // Expand the season if any choice is resolved (save has data for it)
            seasonVm.IsExpanded = seasonVm.Episodes
                .Any(e => e.Choices.Any(c => c.IsResolved));

            Seasons.Add(seasonVm);
        }

        StatusText = $"Loaded decisions for {SelectedSlot.Slot.FileName}";
    }

    private void LoadEmbeddedNames()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("property_names.json"));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    _nameDb.LoadFromJson(reader.ReadToEnd());
                }
            }
        }
        catch
        {
            // Non-critical
        }
    }

    [RelayCommand]
    private void LoadSaves()
    {
        try
        {
            SaveSlots.Clear();
            SelectedSlot = null;

            if (!_saveManager.SaveDirectoryExists)
            {
                StatusText = $"Save directory not found: {_saveManager.SaveDirectory}";
                HasFiles = false;
                return;
            }

            var files = _saveManager.DiscoverFiles();
            if (files.Count == 0)
            {
                StatusText = "No .bundle save files found in save directory.";
                HasFiles = false;
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    var slot = _saveManager.LoadFile(file);
                    SaveSlots.Add(new SaveSlotViewModel(slot, _nameDb));
                }
                catch (Exception ex)
                {
                    SaveSlots.Add(new SaveSlotViewModel(
                        new SaveSlot
                        {
                            FilePath = file,
                            FileName = $"[ERROR] {Path.GetFileName(file)}: {ex.Message}",
                            OuterHeader = new MetaStreamHeader(),
                            FileTable = [],
                        }, _nameDb));
                }
            }

            HasFiles = true;
            StatusText = $"Loaded {files.Count} file(s) from {_saveManager.SaveDirectory}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading saves: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select TWD Save Directory"
        };

        if (dialog.ShowDialog() == true)
        {
            _saveManager.SetSaveDirectory(dialog.FolderName);
            OnPropertyChanged(nameof(SaveDirectory));
            LoadSaves();
        }
    }

    [RelayCommand]
    private void SaveChanges()
    {
        if (SelectedSlot == null)
        {
            StatusText = "No save file selected.";
            return;
        }

        try
        {
            _saveManager.SaveFile(SelectedSlot.Slot);
            StatusText = $"Saved {SelectedSlot.Slot.FileName} (backup created)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CreateNewSave()
    {
        try
        {
            if (!_saveManager.SaveDirectoryExists)
            {
                // Ask user to pick a directory first
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select save directory for new save"
                };
                if (folderDialog.ShowDialog() != true)
                    return;
                _saveManager.SetSaveDirectory(folderDialog.FolderName);
                OnPropertyChanged(nameof(SaveDirectory));
            }

            var dialog = new Views.NewSaveDialog { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() != true)
                return;

            var fileName = dialog.FileName;
            var seasonKey = dialog.SelectedSeasonKey;
            var episode = dialog.SelectedEpisode;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                StatusText = "No file name specified.";
                return;
            }

            if (!fileName.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                fileName += ".bundle";

            var filePath = Path.Combine(_saveManager.SaveDirectory, fileName);
            if (File.Exists(filePath))
            {
                var result = MessageBox.Show(
                    $"{fileName} already exists. Overwrite?",
                    "File Exists",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            var slot = _saveManager.CreateNewSave(fileName, seasonKey, episode);
            var vm = new SaveSlotViewModel(slot, _nameDb);
            SaveSlots.Add(vm);
            SelectedSlot = vm;
            HasFiles = true;
            StatusText = $"Created new save: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error creating save: {ex.Message}";
            MessageBox.Show($"Failed to create save:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(SearchText));
    }
}
