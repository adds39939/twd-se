using System.Text;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Chapters;

public sealed class SceneMap(IReadOnlyList<SceneScript> scripts, string sceneDirectory)
{
    private const string DialogExtension = ".dlog";
    private const string SceneExtension = ".scene";

    private readonly Dictionary<string, string> _sceneText = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ScriptsFor(string dialog)
    {
        var file = Path.GetFileNameWithoutExtension(dialog);
        var name = Base(file);
        var matches = scripts
            .Where(script => name.StartsWith(Base(script.Scene), StringComparison.OrdinalIgnoreCase))
            .GroupBy(script => Base(script.Scene).Length)
            .MaxBy(group => group.Key);

        matches ??= scripts
            .Where(script => Base(script.Scene).StartsWith(name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(script => -Base(script.Scene).Length)
            .MaxBy(group => group.Key);

        if (matches != null)
            return [.. matches.Select(script => script.Script)];

        var named = scripts.Where(script => script.Text.Contains(file, StringComparison.OrdinalIgnoreCase)).Select(script => script.Script).ToList();
        if (named.Count > 0)
            return named;

        var symbol = Encoding.Latin1.GetString(BitConverter.GetBytes(TelltaleCrc64.Compute(file + DialogExtension)));
        return
        [
            .. scripts
                .Where(script => SceneText(script.Scene).Contains(file + DialogExtension, StringComparison.OrdinalIgnoreCase)
                    || SceneText(script.Scene).Contains(symbol, StringComparison.Ordinal))
                .Select(script => script.Script),
        ];
    }

    private string SceneText(string scene)
    {
        if (_sceneText.TryGetValue(scene, out var text))
            return text;

        var path = Path.Combine(sceneDirectory, scene + SceneExtension);
        return _sceneText[scene] = File.Exists(path) ? Encoding.Latin1.GetString(File.ReadAllBytes(path)) : string.Empty;
    }

    private static string Base(string name) => name[(name.IndexOf('_') + 1)..];
}
