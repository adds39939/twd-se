using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.App.ViewModels;

/// <summary>
/// ViewModel for an episode within a season, grouping its choices.
/// </summary>
public partial class EpisodeViewModel : ObservableObject
{
    public EpisodeViewModel(Episode episode, IEnumerable<ChoiceViewModel> choices)
    {
        EpisodeNumber = episode.Number;
        Title = $"Episode {episode.Number}: {episode.Title}";
        ChapterCount = episode.ChapterCount;

        foreach (var c in choices)
            Choices.Add(c);
    }

    public int EpisodeNumber { get; }
    public string Title { get; }
    public int ChapterCount { get; }
    public ObservableCollection<ChoiceViewModel> Choices { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;
}
