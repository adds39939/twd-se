using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Items;

public static class DialogItemChanges
{
    public static List<ItemChange> Read(DialogFile dialog, IReadOnlyList<string> scripts, IReadOnlyCollection<string> items, bool fullNames = false)
    {
        var changes = new List<ItemChange>();
        var visited = new HashSet<ulong>();
        foreach (var item in dialog.Items)
            Add(changes, [.. Scripts(dialog, item.First, visited)], scripts, items, fullNames);

        foreach (var node in dialog.Nodes.Values.Where(node => node.Script != null && !visited.Contains(node.Id)))
            Add(changes, [node.Script!], scripts, items, fullNames);

        return changes;
    }

    private static void Add(List<ItemChange> changes, List<string> texts, IReadOnlyList<string> scripts, IReadOnlyCollection<string> items, bool fullNames)
    {
        var chapter = texts.SelectMany(CheckpointCalls.ChapterIds).FirstOrDefault();
        foreach (var text in texts)
        {
            var added = fullNames ? InventoryCalls.AddedNames(text) : InventoryCalls.Added(text);
            var removed = fullNames ? InventoryCalls.RemovedNames(text) : InventoryCalls.Removed(text);
            changes.AddRange(added.Where(items.Contains).Select(item => new ItemChange(item, false, scripts, chapter)));
            changes.AddRange((InventoryCalls.Clears(text) ? items : removed.Where(items.Contains))
                .Select(item => new ItemChange(item, true, scripts, chapter)));
        }
    }

    private static IEnumerable<string> Scripts(DialogFile dialog, ulong first, HashSet<ulong> visited)
    {
        for (var node = dialog.Find(first); node != null && visited.Add(node.Id); node = dialog.Find(node.Next))
        {
            if (node.Script != null)
                yield return node.Script;

            foreach (var text in node.Branches.SelectMany(branch => Scripts(dialog, branch.First, visited)))
                yield return text;
        }
    }
}
