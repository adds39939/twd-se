using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Cryptography;

namespace TwdSaveEditor.Tools.ExtractScenes.Scenes;

public static class EpisodeScanner
{
    private const string SceneExtension = ".scene";
    private const string DialogExtension = ".dlog";

    public static EpisodeContents? Scan(string archivePath, BlowfishV7 cipher)
    {
        var data = EcttArchive.Read(archivePath, cipher);
        if (data is not { Length: > 0 })
        {
            return null;
        }

        var names = InnerArchive.Parse(data).Keys;
        var scenes = WithExtension(names, SceneExtension).Select(name => Path.GetFileNameWithoutExtension(name));
        var dialogs = WithExtension(names, DialogExtension).Select(name => Path.GetFileName(name));
        return new EpisodeContents(Sorted(scenes), Sorted(dialogs));
    }

    private static IEnumerable<string> WithExtension(IEnumerable<string> names, string extension) =>
        names.Where(name => name.ToLowerInvariant().EndsWith(extension, StringComparison.Ordinal));

    private static List<string> Sorted(IEnumerable<string> values) => values.Distinct().Order(StringComparer.Ordinal).ToList();
}
