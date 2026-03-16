using System.Windows;
using System.Windows.Controls;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.App.Views;

public partial class NewSaveDialog : Window
{
    public string FileName => FileNameBox.Text.Trim();
    public string SelectedSeasonKey { get; private set; } = "s1";
    public int SelectedEpisode { get; private set; } = 1;

    public NewSaveDialog()
    {
        InitializeComponent();

        // Populate seasons
        foreach (var season in SeasonInfo.Seasons)
            SeasonCombo.Items.Add(new ComboBoxItem
            {
                Content = season.Name,
                Tag = season.Key,
            });

        SeasonCombo.SelectedIndex = 0;
    }

    private void SeasonCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeasonCombo.SelectedItem is not ComboBoxItem item) return;

        SelectedSeasonKey = (string)item.Tag;
        var season = SeasonInfo.FindSeason(SelectedSeasonKey);
        if (season == null) return;

        EpisodeCombo.Items.Clear();
        foreach (var ep in season.Episodes)
        {
            EpisodeCombo.Items.Add(new ComboBoxItem
            {
                Content = $"Episode {ep.Number}: {ep.Title}",
                Tag = ep.Number,
            });
        }
        EpisodeCombo.SelectedIndex = 0;

        // Update suggested filename
        var prefix = SelectedSeasonKey switch
        {
            "s1" => "wd1",
            "s1_400days" => "wd1",
            "s2" => "wd2",
            "michonne" => "michonne",
            "s3" => "wd3",
            "s4" => "wd4",
            _ => "wd",
        };
        FileNameBox.Text = $"{prefix}_newsave.bundle";
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (EpisodeCombo.SelectedItem is ComboBoxItem epItem)
            SelectedEpisode = (int)epItem.Tag;

        DialogResult = true;
    }
}
