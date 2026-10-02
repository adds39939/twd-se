using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Dialogs;

public sealed partial class DialogScanner(DialogLoader loader, IReadOnlyDictionary<string, IReadOnlyList<string>> scriptLoaders)
{
    private const string PersistentKey = "Persistent key";
    private const string PersistentValue = "Persistent value";

    public List<SceneTransition> Transitions { get; } = [];

    public List<CheckpointCall> Checkpoints { get; } = [];

    public List<Decision> Decisions { get; } = [];

    public void Scan(string path)
    {
        var dialog = loader.Load(path);
        var tagged = dialog.Nodes.Values.OrderBy(node => node.Id).Select(node => node.UserProps)
            .Concat(dialog.Nodes.Values.SelectMany(node => node.Branches).Concat(dialog.Items).Select(branch => branch.UserProps));
        foreach (var props in tagged)
        {
            if ((props?.Find(PersistentKey) as MetaScalar)?.Text is { Length: > 0 } key)
                Decisions.Add(new Decision(dialog.Name, key, (props.Find(PersistentValue) as MetaScalar)?.Text ?? string.Empty));
        }

        foreach (var node in dialog.Nodes.Values.Where(node => node.Script != null).OrderBy(node => node.Id))
        {
            foreach (Match match in LoadScript().Matches(node.Script!))
                Transitions.Add(new SceneTransition(dialog.Name, node.Id, match.Groups[1].Value));

            foreach (Match match in Call().Matches(node.Script!))
            {
                foreach (var target in scriptLoaders.GetValueOrDefault(match.Groups[1].Value) ?? [])
                    Transitions.Add(new SceneTransition(dialog.Name, node.Id, target));
            }

            foreach (Match match in Checkpoint().Matches(node.Script!))
            {
                Checkpoints.Add(new CheckpointCall(
                    dialog.Name,
                    node.Id,
                    match.Groups[1].Success ? match.Groups[1].Value : null,
                    match.Groups[2].Success ? match.Groups[2].Value : null));
            }
        }
    }

    [GeneratedRegex("\\b(\\w+)\\(\\s*\\)")]
    private static partial Regex Call();

    [GeneratedRegex("LoadScript\\(\\s*\"([^\"]+)\"\\s*\\)")]
    private static partial Regex LoadScript();

    [GeneratedRegex("\\bCheckpoint\\(\\s*(?:\"([^\"]*)\"|nil)?\\s*(?:,\\s*\"([^\"]*)\")?")]
    private static partial Regex Checkpoint();
}
