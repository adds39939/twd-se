using System.Text.RegularExpressions;

namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Scripts;

public static partial class NodeIds
{
    private const char Negation = '~';

    public static List<string> In(string text) => [.. Guid().Matches(text).Select(match => match.Value.ToUpperInvariant())];

    public static List<string> Required(string expression) =>
    [
        .. Guid().Matches(expression)
            .Where(match => match.Index == 0 || expression[match.Index - 1] != Negation)
            .Select(match => match.Value.ToUpperInvariant()),
    ];

    [GeneratedRegex("[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}")]
    private static partial Regex Guid();
}
