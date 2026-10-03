using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Chapters;

public sealed class DialogOwners(EpisodeChapters episode)
{
    private const string ScenePrefix = "adv_";
    private const string DialogPrefix = "env_";

    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    public IEnumerable<string> Of(string dialog)
    {
        var name = Path.GetFileNameWithoutExtension(dialog);
        return episode.Scripts.Where(script => script.Scene != null && (
                script.Dialogs.Contains(dialog, IgnoreCase)
                || episode.SceneDialogs.GetValueOrDefault(script.Scene)?.Contains(dialog, IgnoreCase) == true
                || IsNamedAfter(name, script.Scene)))
            .Select(script => script.Name);
    }

    public IEnumerable<string> NamedLike(string dialog)
    {
        var name = Path.GetFileNameWithoutExtension(dialog);
        return episode.Scripts
            .Where(script => script.Scene != null && Bases(script).Any(prefix => name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)))
            .Select(script => script.Name);
    }

    private static IEnumerable<string> Bases(SceneScript script)
    {
        if (script.Scene!.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase))
        {
            yield return script.Scene[ScenePrefix.Length..];
        }

        var name = Path.GetFileNameWithoutExtension(script.Name);
        if (name.StartsWith(DialogPrefix, StringComparison.OrdinalIgnoreCase))
        {
            yield return name[DialogPrefix.Length..];
        }
    }

    private static bool IsNamedAfter(string dialog, string scene)
    {
        if (!scene.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = DialogPrefix + scene[ScenePrefix.Length..];
        return IgnoreCase.Equals(dialog, expected) || dialog.StartsWith(expected + "_", StringComparison.OrdinalIgnoreCase);
    }
}
