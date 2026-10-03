using System.Globalization;
using System.Text.RegularExpressions;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.UI.Formatting;

public static partial class ResumeLabels
{
    private const string SavedFormat = "d MMM yyyy, HH:mm";

    public static string Episode(IResumePointHandler handler, int number)
    {
        var episode = handler.ResumeEpisodes.FirstOrDefault(e => e.Number == number);
        return episode == null ? $"Episode {number}" : $"Episode {episode.Number}: {episode.Title}";
    }

    public static string Checkpoint(string? checkpoint) =>
        string.IsNullOrEmpty(checkpoint) ? string.Empty : WordBreak().Replace(char.ToUpperInvariant(checkpoint[0]) + checkpoint[1..], " ");

    public static string? Saved(string? savedAt) =>
        DateTime.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var saved)
            ? saved.ToString(SavedFormat, CultureInfo.InvariantCulture)
            : savedAt;

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])|(?<=[A-Za-z])(?=[0-9])")]
    private static partial Regex WordBreak();
}
