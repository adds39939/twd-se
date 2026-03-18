using System.Reflection;
using System.Text.Json;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Provides scene names per episode, loaded from embedded game data.
/// Scene names are what the game stores in autosave metadata_save.p
/// to determine which scene to load on resume.
/// </summary>
public static class SceneDatabase
{
    private static readonly Lazy<Dictionary<string, string[]>> _scenes = new(LoadScenes);

    /// <summary>
    /// Get the list of gameplay scene names for a given episode ID (e.g. "WalkingDead101").
    /// Returns empty array if the episode is not found.
    /// </summary>
    public static string[] GetScenes(string episodeId)
    {
        return _scenes.Value.TryGetValue(episodeId, out var scenes) ? scenes : [];
    }

    /// <summary>
    /// Get all known episode IDs.
    /// </summary>
    public static IEnumerable<string> GetEpisodeIds() => _scenes.Value.Keys;

    private static Dictionary<string, string[]> LoadScenes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("episode_scenes.json", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream) ?? new();
    }
}
