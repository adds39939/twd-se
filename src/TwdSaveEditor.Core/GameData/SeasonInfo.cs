namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Defines the season/episode/chapter structure of TWD: The Telltale Definitive Series.
/// </summary>
public static class SeasonInfo
{
    public static readonly Season[] Seasons =
    [
        new("Season 1", "s1", 5, [
            new(1, "A New Day", 7),
            new(2, "Starved for Help", 7),
            new(3, "Long Road Ahead", 7),
            new(4, "Around Every Corner", 7),
            new(5, "No Time Left", 7),
        ]),
        new("Season 1: 400 Days", "s1_400days", 1, [
            new(1, "400 Days", 6),
        ]),
        new("Season 2", "s2", 5, [
            new(1, "All That Remains", 7),
            new(2, "A House Divided", 7),
            new(3, "In Harm's Way", 7),
            new(4, "Amid the Ruins", 7),
            new(5, "No Going Back", 7),
        ]),
        new("Michonne", "michonne", 3, [
            new(1, "In Too Deep", 6),
            new(2, "Give No Shelter", 6),
            new(3, "What We Deserve", 6),
        ]),
        new("A New Frontier (Season 3)", "s3", 5, [
            new(1, "Ties That Bind - Part One", 6),
            new(2, "Ties That Bind - Part Two", 6),
            new(3, "Above the Law", 6),
            new(4, "Thicker Than Water", 6),
            new(5, "From the Gallows", 6),
        ]),
        new("The Final Season (Season 4)", "s4", 4, [
            new(1, "Done Running", 6),
            new(2, "Suffer the Children", 6),
            new(3, "Broken Toys", 6),
            new(4, "Take Us Back", 6),
        ]),
    ];

    /// <summary>
    /// Find a season by its internal key prefix (e.g., "s1", "s2", "michonne").
    /// </summary>
    public static Season? FindSeason(string key)
    {
        return Seasons.FirstOrDefault(s =>
            s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}

public record Season(string Name, string Key, int EpisodeCount, Episode[] Episodes);
public record Episode(int Number, string Title, int ChapterCount);
