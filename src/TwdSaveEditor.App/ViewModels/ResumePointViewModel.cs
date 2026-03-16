using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.App.ViewModels;

/// <summary>
/// ViewModel for editing the game resume point (season/episode/chapter).
/// Reads resume data from the metadata PropertySet in the bundle.
/// </summary>
public partial class ResumePointViewModel : ObservableObject
{
    private readonly SaveAccessor _accessor;

    public ResumePointViewModel(SaveAccessor accessor)
    {
        _accessor = accessor;

        // Build season list
        foreach (var s in SeasonInfo.Seasons)
            Seasons.Add(new SeasonEntry(s.Key, s.Name));

        // Read current state from metadata
        var episode = accessor.GetMetadataInt(ResumePoint.Keys.CurrentEpisode)
                   ?? accessor.GetMetadataInt(ResumePoint.Keys.EpisodeNumber);
        var chapter = accessor.GetMetadataInt(ResumePoint.Keys.CurrentChapter)
                   ?? accessor.GetMetadataInt(ResumePoint.Keys.ChapterNumber);

        if (episode.HasValue)
            SelectedEpisode = episode.Value;
        if (chapter.HasValue)
            SelectedChapter = chapter.Value;

        // Try to detect current season from metadata
        var seasonVal = accessor.GetMetadataString(ResumePoint.Keys.ActiveSeason);
        if (seasonVal != null)
        {
            var match = Seasons.FirstOrDefault(s => s.Key.Equals(seasonVal, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                SelectedSeasonIndex = Seasons.IndexOf(match);
        }

        UpdateEpisodeList();
        UpdateChapterList();
    }

    public ObservableCollection<SeasonEntry> Seasons { get; } = [];
    public ObservableCollection<EpisodeEntry> Episodes { get; } = [];
    public ObservableCollection<int> Chapters { get; } = [];

    [ObservableProperty]
    private int _selectedSeasonIndex;

    [ObservableProperty]
    private int _selectedEpisode = 1;

    [ObservableProperty]
    private int _selectedChapter = 1;

    [ObservableProperty]
    private bool _isGameComplete;

    public string? SelectedSeasonKey => SelectedSeasonIndex >= 0 && SelectedSeasonIndex < Seasons.Count
        ? Seasons[SelectedSeasonIndex].Key
        : null;

    partial void OnSelectedSeasonIndexChanged(int value)
    {
        UpdateEpisodeList();
        OnPropertyChanged(nameof(SelectedSeasonKey));
    }

    partial void OnSelectedEpisodeChanged(int value)
    {
        UpdateChapterList();
    }

    partial void OnSelectedChapterChanged(int value)
    {
        // Resume point writing will be implemented when metadata writing is added
    }

    partial void OnIsGameCompleteChanged(bool value)
    {
        // Game complete writing will be implemented when metadata writing is added
    }

    private void UpdateEpisodeList()
    {
        Episodes.Clear();
        if (SelectedSeasonKey == null) return;

        var season = SeasonInfo.FindSeason(SelectedSeasonKey);
        if (season == null) return;

        foreach (var ep in season.Episodes)
            Episodes.Add(new EpisodeEntry(ep.Number, $"Ep {ep.Number}: {ep.Title}"));

        if (SelectedEpisode > Episodes.Count)
            SelectedEpisode = 1;

        UpdateChapterList();
    }

    private void UpdateChapterList()
    {
        Chapters.Clear();
        if (SelectedSeasonKey == null) return;

        var season = SeasonInfo.FindSeason(SelectedSeasonKey);
        if (season == null) return;

        var ep = season.Episodes.FirstOrDefault(e => e.Number == SelectedEpisode);
        if (ep == null) return;

        for (int i = 1; i <= ep.ChapterCount; i++)
            Chapters.Add(i);

        if (SelectedChapter > Chapters.Count)
            SelectedChapter = 1;
    }
}

public record SeasonEntry(string Key, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public record EpisodeEntry(int Number, string DisplayName)
{
    public override string ToString() => DisplayName;
}
