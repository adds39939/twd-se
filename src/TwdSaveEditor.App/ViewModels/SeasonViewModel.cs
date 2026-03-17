using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.App.ViewModels;

/// <summary>
/// ViewModel for an entire season, containing episodes and their choices.
/// </summary>
public partial class SeasonViewModel : ObservableObject
{
    public SeasonViewModel(Season season)
    {
        Name = season.Name;
        Key = season.Key;
        EpisodeCount = season.EpisodeCount;
    }

    public string Name { get; }
    public string Key { get; }
    public int EpisodeCount { get; }

    public ObservableCollection<EpisodeViewModel> Episodes { get; } = [];

    /// <summary>Whether any episode in this season has choice definitions.</summary>
    public bool HasChoiceDefinitions => Episodes.Any(e => e.Choices.Count > 0);

    [ObservableProperty]
    private bool _isExpanded;
}
