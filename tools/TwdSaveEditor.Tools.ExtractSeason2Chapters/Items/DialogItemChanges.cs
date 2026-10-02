using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Items;

public static class DialogItemChanges
{
    public static List<ItemChange> Read(DialogFile dialog, IReadOnlyList<string> scripts, IReadOnlyCollection<string> items)
    {
        var changes = new List<ItemChange>();
        var visited = new HashSet<ulong>();
        foreach (var item in dialog.Items)
            Add(changes, [.. Scripts(dialog, item.First, visited)], scripts, items);

        foreach (var node in dialog.Nodes.Values.Where(node => node.Script != null && !visited.Contains(node.Id)))
            Add(changes, [node.Script!], scripts, items);

        return changes;
    }

    private static void Add(List<ItemChange> changes, List<string> texts, IReadOnlyList<string> scripts, IReadOnlyCollection<string> items)
    {
        var chapter = texts.SelectMany(CheckpointCalls.ChapterIds).FirstOrDefault();
        foreach (var text in texts)
        {
            changes.AddRange(InventoryCalls.Added(text).Where(items.Contains).Select(item => new ItemChange(item, false, scripts, chapter)));
            changes.AddRange((InventoryCalls.Clears(text) ? items : InventoryCalls.Removed(text).Where(items.Contains))
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
