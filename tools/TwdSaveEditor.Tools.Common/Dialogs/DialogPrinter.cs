using System.Globalization;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Dialogs;

public sealed class DialogPrinter(SymbolNames names, TextWriter output)
{
    private static readonly string[] Comparisons = ["==", "!=", "<", "<=", ">", ">="];
    private static readonly string[] Actions = ["=", "+=", "-="];

    private readonly HashSet<ulong> _printed = [];

    public void Print(DialogFile dialog)
    {
        _printed.Clear();
        Line(0, $"dialog {dialog.Name}");
        foreach (var folder in dialog.Folders)
        {
            Line(0, $"folder {names.Describe(folder.Name)}");
            foreach (var item in folder.Children)
            {
                PrintBranch(dialog, item, 1);
            }
        }

        var unreached = dialog.Nodes.Values.Where(node => !_printed.Contains(node.Id) && dialog.Find(node.Previous) == null).ToList();
        if (unreached.Count > 0)
        {
            Line(0, "unreferenced");
        }

        foreach (var node in unreached)
        {
            PrintChain(dialog, node.Id, 1);
        }
    }

    public string Describe(DialogRule rule)
    {
        var parts = new List<string>();
        if (!rule.Conditions.IsEmpty)
        {
            parts.Add($"if {Describe(rule.Conditions, false)}");
        }

        if (!rule.Actions.IsEmpty)
        {
            parts.Add($"then {Describe(rule.Actions, true)}");
        }

        if (!rule.Otherwise.IsEmpty)
        {
            parts.Add($"else {Describe(rule.Otherwise, true)}");
        }

        return string.Join(" ", parts);
    }

    public string Describe(DialogLogicEntry entry, bool action)
    {
        var name = $"{entry.Target}[{names.Describe(entry.Key)}]";
        var value = FormatValue(entry.Value);
        if (action)
        {
            return $"{name} {Pick(Actions, entry.Action)} {value}";
        }

        var text = $"{name} {Pick(Comparisons, entry.Comparison)} {value}";
        return entry.Negate ? $"not ({text})" : text;
    }

    private string Describe(DialogLogicGroup group, bool action)
    {
        var separator = action ? "; " : group.Operator == DialogLogicGroup.Or ? " or " : " and ";
        var parts = group.Entries.Select(entry => Describe(entry, action))
            .Concat(group.Groups.Where(child => !child.IsEmpty).Select(child => $"({Describe(child, action)})"))
            .ToList();

        return string.Join(separator, parts);
    }

    private void PrintBranch(DialogFile dialog, DialogBranch branch, int indent)
    {
        var text = $"{branch.Group} {names.Describe(branch.Name)} #{branch.Id:X16}{Conditions(branch.Visibility, branch.VisibilityScript)}";
        foreach (var condition in branch.Conditions.Where(condition => !condition.IsEmpty))
        {
            text += $" [when {Describe(condition)}]";
        }

        Line(indent, text + UserProps(branch.UserProps));
        PrintChain(dialog, branch.First, indent + 1);
    }

    private void PrintChain(DialogFile dialog, ulong id, int indent)
    {
        while (dialog.Find(id) is { } node)
        {
            if (!_printed.Add(node.Id))
            {
                Line(indent, $"-> #{node.Id:X16}");
                return;
            }

            Line(indent, Describe(node));
            foreach (var branch in node.Branches)
            {
                PrintBranch(dialog, branch, indent + 1);
            }

            id = node.Next;
        }
    }

    private string Describe(DialogNode node)
    {
        var text = $"{node.Kind} #{node.Id:X16}";
        if (node.Name != 0)
        {
            text += $" {names.Describe(node.Name)}";
        }

        text += Conditions(node.Visibility, node.VisibilityScript);
        if (node.Rule is { IsEmpty: false } rule)
        {
            text += $": {Describe(rule)}";
        }

        if (node.Script != null)
        {
            text += $": {node.Script.ReplaceLineEndings(" ")}";
        }

        if (node.Jump is { } jump)
        {
            text += $": {DescribeJump(jump)}";
        }

        if (node.Chore != 0)
        {
            text += $" chore={names.Describe(node.Chore)}";
        }

        return text + UserProps(node.UserProps);
    }

    private string DescribeJump(DialogJump jump)
    {
        var target = jump.TargetClass switch
        {
            DialogJump.ToParent => "parent",
            DialogJump.ToNodeAfterParentWait => "after parent wait",
            _ => jump.TargetName != 0 ? names.Describe(jump.TargetName) : $"#{jump.Target:X16}",
        };
        var behaviour = jump.Behaviour switch
        {
            DialogJump.JumpExecuteAndReturn => "call",
            DialogJump.Return => "return",
            _ => "goto",
        };
        var dialog = jump.Dialog != 0 ? $" in {names.Describe(jump.Dialog)}" : string.Empty;
        return $"{behaviour} {target}{dialog}";
    }

    private string Conditions(DialogRule? visibility, string script)
    {
        var text = string.Empty;
        if (visibility is { IsEmpty: false })
        {
            text += $" [visible {Describe(visibility)}]";
        }

        if (script.Length > 0)
        {
            text += $" [visible script {script.ReplaceLineEndings(" ")}]";
        }

        return text;
    }

    private string UserProps(MetaPropertySet? props)
    {
        if (props == null || props.Properties.Count == 0)
        {
            return string.Empty;
        }

        return " {" + string.Join(", ", props.Properties.Select(property => $"{names.Describe(property.Key)}: {FormatValue(property.Value)}")) + "}";
    }

    private string FormatValue(MetaNode node) => node switch
    {
        MetaSymbol symbol => names.Describe(symbol.Hash),
        MetaScalar { Value: string text } => TextFormat.QuoteString(text),
        MetaScalar { Value: bool flag } => flag ? "true" : "false",
        MetaScalar { Value: float number } => number.ToString("R", CultureInfo.InvariantCulture),
        MetaScalar scalar => Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty,
        MetaObject value => value.Type,
        _ => node.GetType().Name,
    };

    private static string Pick(string[] values, int index) => index >= 0 && index < values.Length ? values[index] : $"?{index}";

    private void Line(int indent, string text) => output.WriteLine(new string(' ', indent * 2) + text);
}
