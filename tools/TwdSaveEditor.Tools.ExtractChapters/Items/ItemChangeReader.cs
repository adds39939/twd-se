using System.Globalization;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Items;

public sealed partial class ItemChangeReader(IReadOnlyList<ItemDefinition> items)
{
    private const int Assign = 0;
    private const int Increase = 1;

    private readonly Dictionary<(string Agent, ulong Key), ItemDefinition> _byKey =
        items.ToDictionary(item => (item.Agent, TelltaleCrc64.Compute(item.Key)));

    public List<ItemChange> Changes { get; } = [];

    public Dictionary<string, int> Increases { get; } = [];

    public void ReadDialog(DialogFile dialog, IReadOnlyList<string> owners)
    {
        var actions = dialog.Nodes.Values.OrderBy(node => node.Id)
            .Where(node => node.Rule != null)
            .SelectMany(node => node.Rule!.Actions.AllEntries.Concat(node.Rule.Otherwise.AllEntries));

        foreach (var action in actions)
        {
            if (!_byKey.TryGetValue((action.Target, action.Key), out var item))
                continue;

            if (action.Action == Increase)
                Increases[item.Key] = Increases.GetValueOrDefault(item.Key) + 1;

            Changes.Add(new ItemChange(item.Key, action.Action == Assign ? !Truthy(action.Value) : action.Action != Increase, owners, null));
        }
    }

    public void ReadScript(string name, string text)
    {
        foreach (Match call in Call().Matches(text))
        {
            if (items.Any(item => item.Key == call.Groups["key"].Value))
                Changes.Add(new ItemChange(call.Groups["key"].Value, call.Groups["verb"].Value == "Remove", [name], null));
        }
    }

    private static bool Truthy(MetaNode value) => value is MetaScalar scalar && scalar.Value switch
    {
        bool flag => flag,
        string text => text.Length > 0,
        IConvertible number => number.ToDouble(CultureInfo.InvariantCulture) > 0,
        _ => false,
    };

    [GeneratedRegex("\\bWDInventory_(?<verb>Add|Remove)Item\\(\\s*\"(?<key>[^\"]+)\"")]
    private static partial Regex Call();
}
