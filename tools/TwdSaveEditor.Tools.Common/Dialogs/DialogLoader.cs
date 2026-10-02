using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed class DialogLoader(MetaReader reader)
{
    public const string RootType = "Dlg";

    private static readonly (string Member, string Group)[] BranchMembers =
    [
        ("mChoices", "choice"), ("mPreChoice", "before choices"), ("mPostChoice", "after choices"), ("mCases", "case"),
        ("mElements", "element"), ("mPElements", "parallel"), ("mCohorts", "cohort"),
    ];

    public DialogFile Load(string path)
    {
        var document = reader.Read(File.ReadAllBytes(path), RootType);
        if (document?.Root is not MetaObject root)
            throw new MetaFormatException($"{Path.GetFileName(path)}: {document?.Error ?? "not a MetaStream"}");

        var folders = Items(root.Find("folders")).Select(LoadFolder).ToList();
        var nodes = Items(root.Find("nodes")).Select(LoadNode).ToDictionary(node => node.Id);
        return new DialogFile(Path.GetFileName(path), folders, nodes);
    }

    private static DialogFolder LoadFolder(MetaObject folder) => new(
        ObjectId(folder),
        Symbol(folder.Find("mName")),
        LoadBranches(folder.Find("Baseclass_DlgChildSet"), "item"));

    private static DialogNode LoadNode(MetaObject source)
    {
        var node = Child(source, "Baseclass_DlgNode") ?? source;
        var conditions = VisibilityOf(node);
        var branches = BranchMembers.SelectMany(entry => LoadBranches(source.Find(entry.Member), entry.Group)).ToList();
        var rule = Child(source, "mRule") is { } value ? LoadRule(value) : null;
        var jump = source.Type == "DlgNodeJump"
            ? new DialogJump(LinkId(source.Find("mJumpToLink")), Symbol(source.Find("mJumpToName")), Int(source.Find("mJumpTargetClass")),
                Int(source.Find("mJumpBehavior")), Symbol(source.Find("mhJumpToDlg")))
            : null;

        return new DialogNode(
            ObjectId(node),
            source.Type.Replace("DlgNode", string.Empty, StringComparison.Ordinal),
            Symbol(node.Find("mName")),
            node.FindUInt32("mFlags"),
            LinkId(node.Find("mPrev")),
            LinkId(node.Find("mNext")),
            conditions.Rule,
            conditions.Script,
            UserPropsOf(node),
            branches,
            rule,
            (source.Find("mScriptText") as MetaScalar)?.Text,
            jump,
            Symbol(source.Find("mhChore") ?? source.Find("mChore") ?? source.Find("mhIdle")),
            source);
    }

    private static List<DialogBranch> LoadBranches(MetaNode? set, string group)
    {
        if (set is not MetaObject value)
            return [];

        var childSet = value.Type == "DlgChildSet" ? value : Child(value, "Baseclass_DlgChildSet");
        return Items(childSet?.Find("children")).Select(child => LoadBranch(child, group)).ToList();
    }

    private static DialogBranch LoadBranch(MetaObject source, string group)
    {
        var child = Child(source, "Baseclass_DlgChild") ?? source;
        var head = Child(child, "Baseclass_DlgChainHead") ?? child;
        var visibility = VisibilityOf(child);
        var conditions = Items(Child(source, "Baseclass_DlgConditionSet")?.Find("conditions"))
            .Select(condition => Child(condition, "mRule"))
            .OfType<MetaObject>()
            .Select(LoadRule)
            .ToList();

        return new DialogBranch(
            ObjectId(head),
            source.Type,
            group,
            Symbol(child.Find("mName")),
            LinkId(head.Find("mLink")),
            visibility.Rule,
            visibility.Script,
            conditions,
            UserPropsOf(child));
    }

    private static (DialogRule? Rule, string Script) VisibilityOf(MetaObject owner)
    {
        var conditions = Child(Child(owner, "Baseclass_DlgVisibilityConditionsOwner"), "mVisCond");
        var rule = Child(conditions, "rule") is { } value ? LoadRule(value) : null;
        return (rule, (conditions?.Find("mScriptVisCond") as MetaScalar)?.Text ?? string.Empty);
    }

    private static MetaPropertySet? UserPropsOf(MetaObject owner) =>
        Child(Child(owner, "Baseclass_DlgObjectPropsOwner"), "mDlgObjectProps")?.Find("userProps") as MetaPropertySet;

    private static DialogRule LoadRule(MetaObject rule) => new(
        LoadGroup(Child(rule, "mConditions")),
        LoadGroup(Child(rule, "mActions")),
        LoadGroup(Child(rule, "mElse")));

    private static DialogLogicGroup LoadGroup(MetaObject? group)
    {
        if (group == null)
            return new DialogLogicGroup(DialogLogicGroup.And, DialogLogicGroup.And, [], []);

        var entries = new List<DialogLogicEntry>();
        if (group.Find("mItems") is MetaMap items)
        {
            foreach (var (target, item) in items.StringEntries)
            {
                if (item is MetaObject value)
                    entries.AddRange(LoadEntries(target, value));
            }
        }

        return new DialogLogicGroup(
            Int(group.Find("mOperator")),
            Int(group.Find("mGroupOperator")),
            entries,
            Items(group.Find("mLogicGroups")).Select(LoadGroup).ToList());
    }

    private static IEnumerable<DialogLogicEntry> LoadEntries(string target, MetaObject item)
    {
        if (item.Find("Baseclass_PropertySet") is not MetaPropertySet properties)
            yield break;

        var negated = SymbolMap(item.Find("mKeyNegateList"));
        var comparisons = SymbolMap(item.Find("mKeyComparisonList"));
        var actions = SymbolMap(item.Find("mKeyActionList"));
        foreach (var property in properties.Properties)
        {
            yield return new DialogLogicEntry(
                target,
                property.Key,
                property.Type,
                property.Value,
                negated.GetValueOrDefault(property.Key) is MetaScalar { Value: true },
                Int(comparisons.GetValueOrDefault(property.Key)),
                Int(actions.GetValueOrDefault(property.Key)));
        }
    }

    private static Dictionary<ulong, MetaNode> SymbolMap(MetaNode? node)
    {
        var result = new Dictionary<ulong, MetaNode>();
        if (node is MetaMap map)
        {
            foreach (var (key, value) in map.Entries)
            {
                if (key is MetaSymbol symbol)
                    result[symbol.Hash] = value;
            }
        }

        return result;
    }

    private static IEnumerable<MetaObject> Items(MetaNode? node) => node is MetaList list ? list.Items.OfType<MetaObject>() : [];

    private static MetaObject? Child(MetaObject? owner, string name) => owner?.Find(name) as MetaObject;

    private static ulong ObjectId(MetaObject owner) =>
        Symbol(Child(Child(owner, "Baseclass_DlgObjIDOwner"), "mDlgObjID")?.Find("mID"));

    private static ulong LinkId(MetaNode? link) => link is MetaObject value ? ObjectId(value) : 0;

    private static ulong Symbol(MetaNode? node) => node is MetaSymbol symbol ? symbol.Hash : 0;

    private static int Int(MetaNode? node) => node is MetaScalar { Value: int value } ? value : 0;
}
