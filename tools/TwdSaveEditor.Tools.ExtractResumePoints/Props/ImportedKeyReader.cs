using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Props;

public sealed class ImportedKeyReader(MetaReader reader)
{
    public const int FirstEpisode = 101;
    public const int LastEpisode = 106;

    public SortedDictionary<int, List<string>>? Read(string directory)
    {
        var keys = new SortedDictionary<int, List<string>>();
        for (var episode = FirstEpisode; episode <= LastEpisode; episode++)
        {
            var path = Path.Combine(directory, $"prefs_persistent_{episode}.prop");
            if (!File.Exists(path) || reader.ReadPropertySet(File.ReadAllBytes(path))?.Find($"Persistent - {episode} - Key Names") is not MetaList names)
                return null;

            keys[episode] = [.. names.Strings];
        }

        return keys;
    }
}
