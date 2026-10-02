using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Dialogs;

public sealed class StoryBoardReader(SymbolNames names)
{
    public List<StoryAction> Read(DialogFile dialog, string lastStoryKey)
    {
        var key = TelltaleCrc64.Compute(lastStoryKey);
        var actions = new List<StoryAction>();
        foreach (var branch in dialog.Nodes.Values.OrderBy(node => node.Id).SelectMany(node => node.Branches))
        {
            var condition = branch.Visibility?.Conditions.AllEntries.FirstOrDefault(entry => entry.Key == key);
            if (condition?.Value is not MetaScalar { Value: int story } || story <= 0)
                continue;

            var visited = new HashSet<ulong>();
            for (var id = branch.First; dialog.Find(id) is { } node && visited.Add(id); id = node.Next)
            {
                foreach (var entry in node.Rule?.Actions.AllEntries ?? [])
                {
                    if (entry.Value is MetaScalar { Value: bool flag } && names.Find(entry.Key) is { } name)
                        actions.Add(new StoryAction(story, entry.Target, name, flag));
                }
            }
        }

        return actions;
    }
}
