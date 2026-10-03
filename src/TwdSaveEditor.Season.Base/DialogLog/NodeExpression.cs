using System.Text.RegularExpressions;

namespace TwdSaveEditor.Season.Base.DialogLog;

public static partial class NodeExpression
{
    private const char Not = '~';
    private const char And = '&';
    private const char Or = '|';
    private const string True = "t";
    private const string False = "f";

    public static IReadOnlyList<ulong> Nodes(string expression) =>
        [.. Node().Matches(expression).Select(match => DialogLogEditor.NodeSymbol(match.Value)).Distinct()];

    public static bool Evaluate(string expression, IReadOnlySet<ulong> seen)
    {
        var rest = expression.Trim();
        var lastOperator = And;
        var lastValue = true;
        while (rest.Length > 0)
        {
            var (first, op, second) = Split(rest);
            var inverse = false;
            while (first.StartsWith(Not))
            {
                inverse = !inverse;
                first = first[1..].TrimStart();
            }

            var value = first switch
            {
                ['(', .., ')'] => Evaluate(first[1..^1], seen),
                True => true,
                False => false,
                _ => seen.Contains(DialogLogEditor.NodeSymbol(first)),
            };

            value ^= inverse;
            value = lastOperator == And ? lastValue && value : lastValue || value;
            if (op == null || (value && op == Or) || (!value && op == And))
            {
                return value;
            }

            lastOperator = op.Value;
            lastValue = value;
            rest = second;
        }

        return false;
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

    [GeneratedRegex("[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}")]
    private static partial Regex Node();
}
