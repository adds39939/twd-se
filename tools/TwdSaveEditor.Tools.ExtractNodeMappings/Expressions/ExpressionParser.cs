using System.Numerics;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractNodeMappings.Model;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.Expressions;

public static partial class ExpressionParser
{
    public static Expression Parse(string expression)
    {
        if (expression is "" or "{}")
            return new Expression(ExpressionKind.Empty);
        if (expression == "{0}")
            return new Expression(ExpressionKind.Zero);

        var inner = TextFormat.Trim(expression);
        if (inner.StartsWith('{') && inner.EndsWith('}'))
            inner = TextFormat.Trim(inner[1..^1]);

        if (GuidPattern().IsMatch(inner))
            return new Expression(ExpressionKind.Guid, Guid: inner);

        if (inner.Contains('|'))
        {
            var parts = inner.Split('|').Select(part => TextFormat.Trim(part).Trim('{', ' ', '}')).ToList();
            if (parts.All(part => GuidPattern().IsMatch(part)))
                return new Expression(ExpressionKind.CompoundGuid, Guids: parts);
        }

        if (inner.Length > 0 && inner.All(char.IsAsciiDigit))
            return new Expression(ExpressionKind.Decimal, Decimal: BigInteger.Parse(inner));

        if (inner.Contains("||") || inner.Contains("&&"))
            return new Expression(ExpressionKind.CompoundLogic);

        return new Expression(ExpressionKind.Unknown);
    }

    [GeneratedRegex("^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
    private static partial Regex GuidPattern();
}
