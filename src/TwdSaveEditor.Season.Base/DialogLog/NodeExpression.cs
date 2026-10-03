using System.Text.Json.Serialization;

namespace TwdSaveEditor.Season.Base.DialogLog;

[JsonConverter(typeof(NodeExpressionJsonConverter))]
public sealed record NodeExpression
{
    private const char Not = '~';
    private const char And = '&';
    private const char Or = '|';
    private const string True = "t";
    private const string False = "f";

    private readonly IReadOnlyList<(Func<IReadOnlySet<ulong>, bool> Term, char? Operator)> _terms;

    private NodeExpression(string text)
    {
        var nodes = new List<ulong>();
        Text = text;
        _terms = Parse(text, nodes);
        Nodes = [.. nodes.Distinct()];
    }

    public static NodeExpression Empty { get; } = new(string.Empty);

    public string Text { get; }

    public IReadOnlyList<ulong> Nodes { get; }

    public bool IsEmpty => Text.Length == 0;

    public static NodeExpression Parse(string text) => text.Length == 0 ? Empty : new(text);

    public bool Evaluate(IReadOnlySet<ulong> seen) => Evaluate(_terms, seen);

    public bool Equals(NodeExpression? other) => other is not null && Text == other.Text;

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    private static bool Evaluate(IReadOnlyList<(Func<IReadOnlySet<ulong>, bool> Term, char? Operator)> terms, IReadOnlySet<ulong> seen)
    {
        foreach (var (term, op) in terms)
        {
            var value = term(seen);
            if (op == null || (value && op == Or) || (!value && op == And))
            {
                return value;
            }
        }

        return false;
    }

    private static List<(Func<IReadOnlySet<ulong>, bool> Term, char? Operator)> Parse(string expression, List<ulong> nodes)
    {
        var terms = new List<(Func<IReadOnlySet<ulong>, bool>, char?)>();
        var rest = expression.Trim();
        while (rest.Length > 0)
        {
            var (first, op, second) = Split(rest);
            terms.Add((Term(first, nodes), op));
            if (op == null)
            {
                break;
            }

            rest = second;
        }

        return terms;
    }

    private static Func<IReadOnlySet<ulong>, bool> Term(string text, List<ulong> nodes)
    {
        var inverse = false;
        while (text.StartsWith(Not))
        {
            inverse = !inverse;
            text = text[1..].TrimStart();
        }

        Func<IReadOnlySet<ulong>, bool> term = text switch
        {
            ['(', .., ')'] => Group(Parse(text[1..^1], nodes)),
            True => _ => true,
            False => _ => false,
            _ => Node(DialogLogEditor.NodeSymbol(text), nodes),
        };
        return inverse ? seen => !term(seen) : term;
    }

    private static Func<IReadOnlySet<ulong>, bool> Group(IReadOnlyList<(Func<IReadOnlySet<ulong>, bool> Term, char? Operator)> terms) =>
        seen => Evaluate(terms, seen);

    private static Func<IReadOnlySet<ulong>, bool> Node(ulong node, List<ulong> nodes)
    {
        nodes.Add(node);
        return seen => seen.Contains(node);
    }

    private static (string First, char? Operator, string Second) Split(string text)
    {
        var position = 0;
        while (position < text.Length && (text[position] == Not || char.IsWhiteSpace(text[position])))
        {
            position++;
        }

        if (position < text.Length && text[position] == '(')
        {
            for (var depth = 1; depth > 0 && ++position < text.Length;)
            {
                depth += text[position] switch { '(' => 1, ')' => -1, _ => 0 };
            }
        }

        var op = text.IndexOfAny([And, Or], Math.Min(position, text.Length));
        return op < 0 ? (text.Trim(), null, string.Empty) : (text[..op].Trim(), text[op], text[(op + 1)..].Trim());
    }
}
