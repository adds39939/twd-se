using System.Reflection;
using System.Text.Json;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Resources;

public static class EmbeddedSeasonData
{
    private const string ChoicesSuffix = ".choices.json";
    private const string ScenesSuffix = ".scenes.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ChoiceDefinition> LoadChoices(Assembly assembly, string seasonKey)
    {
        var choices = new List<ChoiceDefinition>();

        foreach (var stream in OpenResources(assembly, ChoicesSuffix))
        {
            using (stream)
            {
                var defs = JsonSerializer.Deserialize<List<ChoiceDefinition>>(stream, JsonOptions);
                if (defs != null)
                {
                    choices.AddRange(defs.Where(c => c.SeasonKey.Equals(seasonKey, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        return choices.AsReadOnly();
    }

    public static IReadOnlyDictionary<string, string[]> LoadScenes(Assembly assembly)
    {
        var scenes = new Dictionary<string, string[]>();

        foreach (var stream in OpenResources(assembly, ScenesSuffix))
        {
            using (stream)
            {
                var episodes = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream);
                if (episodes == null)
                {
                    continue;
                }

                foreach (var (episodeId, episodeScenes) in episodes)
                {
                    scenes[episodeId] = episodeScenes;
                }
            }
        }

        return scenes;
    }

    private static IEnumerable<Stream> OpenResources(Assembly assembly, string suffix)
    {
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                yield return stream;
            }
        }
    }
}
