using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Dialogs;

public sealed class DialogIndex(DialogLoader loader)
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

            foreach (var chapterId in CheckpointCalls.ChapterIds(node.Script))
                Marks.Add(new ChapterMark(dialog.Name, chapterId));
        }

        foreach (var item in dialog.Items)
            _owners.TryAdd(item.Id, dialog.Name);
    }

    public string? Owner(ulong node) => _owners.GetValueOrDefault(node);
}
