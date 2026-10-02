using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Dialogs;

public sealed partial class DialogIndex(DialogLoader loader)
{
    private readonly Dictionary<ulong, string> _owners = [];

    public List<ChapterMark> Marks { get; } = [];

    public void Scan(string path)
    {
        var dialog = loader.Load(path);
        foreach (var node in dialog.Nodes.Values)
        {
            _owners.TryAdd(node.Id, dialog.Name);
            foreach (var branch in node.Branches)
                _owners.TryAdd(branch.Id, dialog.Name);

            if (node.Script == null)
                continue;

            foreach (Match match in Checkpoint().Matches(node.Script))
                Marks.Add(new ChapterMark(dialog.Name, match.Groups[1].Value));
        }

        foreach (var item in dialog.Items)
            _owners.TryAdd(item.Id, dialog.Name);
    }

    public string? Owner(ulong node) => _owners.GetValueOrDefault(node);

    [GeneratedRegex("\\bCheckpoint\\(\\s*\"[^\"]*\"\\s*,\\s*\"([^\"]+)\"\\s*\\)")]
    private static partial Regex Checkpoint();
}
