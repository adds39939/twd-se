using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class SceneScriptReader
{
    public static SceneScript? Read(string path)
    {
        var text = File.ReadAllText(path);
        return Script().Match(text) is { Success: true } script && Scene().Match(text) is { Success: true } scene
            ? new SceneScript(script.Groups[1].Value, scene.Groups[1].Value, text)
            : null;
    }

    public static bool SetupIsDeveloperOnly(string text) => DeveloperSetup().IsMatch(text);

    [GeneratedRegex("if IsToolBuild\\(\\) then\\s+Callback_OnLogicReady:Add\\(OnLogicReady\\)")]
    private static partial Regex DeveloperSetup();

    [GeneratedRegex("^local kScript = \"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex Script();

    [GeneratedRegex("^local kScene = \"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex Scene();
}
