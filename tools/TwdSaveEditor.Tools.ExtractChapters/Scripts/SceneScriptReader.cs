using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Scripts;

public static partial class SceneScriptReader
{
    public static SceneScript Read(string path)
    {
        var text = File.ReadAllText(path);
        var scene = Scene().Match(text) is { Success: true } match ? match.Groups[1].Value : null;
        return new SceneScript(
            Path.GetFileName(path),
            scene,
            [.. Dialog().Matches(text).Select(dialog => dialog.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. LoadScript().Matches(text).Select(script => script.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    [GeneratedRegex("^local kScene = \"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex Scene();

    [GeneratedRegex("\"([\\w]+\\.dlog)\"")]
    private static partial Regex Dialog();

    [GeneratedRegex("LoadScript\\(\"([^\"]+)\"\\)")]
    private static partial Regex LoadScript();
}
