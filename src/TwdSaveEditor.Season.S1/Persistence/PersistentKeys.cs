namespace TwdSaveEditor.Season.S1.Persistence;

public static class PersistentKeys
{
    public const int FirstEpisode = 101;
    public const int LastEpisode = 106;

    public static string SlotKey(int persistentEpisode, string key) => $"Persistent - {persistentEpisode} - {key}";

    public static string TrackerContainer(int persistentEpisode) => $"Episode {persistentEpisode}";

    public static string TrackerPrefix(string key) => $"{TrackerToken(key)} - ";

    public static string TrackerEntry(string key, string value) => TrackerPrefix(key) + TrackerToken(value);

    private static string TrackerToken(string text) => text.ToLowerInvariant().Replace(' ', '_');
}
