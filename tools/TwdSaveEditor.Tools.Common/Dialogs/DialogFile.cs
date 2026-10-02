namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed record DialogFile(string Name, IReadOnlyList<DialogFolder> Folders, IReadOnlyDictionary<ulong, DialogNode> Nodes)
{
    public IEnumerable<DialogBranch> Items => Folders.SelectMany(folder => folder.Children);

    public DialogNode? Find(ulong id) => Nodes.GetValueOrDefault(id);
}
