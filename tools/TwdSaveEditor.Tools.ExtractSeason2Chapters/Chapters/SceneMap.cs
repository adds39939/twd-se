using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Chapters;

public sealed class SceneMap(IReadOnlyList<SceneScript> scripts)
{
    public IReadOnlyList<string> ScriptsFor(string dialog)
    {
        var name = Base(Path.GetFileNameWithoutExtension(dialog));
        var matches = scripts
            .Where(script => name.StartsWith(Base(script.Scene), StringComparison.OrdinalIgnoreCase))
            .GroupBy(script => Base(script.Scene).Length)
            .MaxBy(group => group.Key);

        matches ??= scripts
            .Where(script => Base(script.Scene).StartsWith(name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(script => -Base(script.Scene).Length)
            .MaxBy(group => group.Key);

        return matches?.Select(script => script.Script).ToList()
            ?? [.. scripts.Where(script => script.Text.Contains(Path.GetFileNameWithoutExtension(dialog), StringComparison.OrdinalIgnoreCase)).Select(script => script.Script)];
    }

    private static string Base(string name) => name[(name.IndexOf('_') + 1)..];
}
