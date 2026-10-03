using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Text;

public static class TextSearch
{
    private const int ExcerptLength = 120;

    public const string GuidPattern = "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}";

    public static IEnumerable<string> LinesContaining(string text, params string[] keywords) =>
        text.Split('\n').Where(line => keywords.All(line.ToLowerInvariant().Contains));

    public static string Excerpt(string line) => TextFormat.Truncate(TextFormat.Trim(line), ExcerptLength);

    public static void AddOccurrences(List<string> details, string lowerText, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var count = TextFormat.CountOccurrences(lowerText, keyword);
            if (count > 0)
            {
                details.Add($"  '{keyword}' occurrences: {count}");
            }
        }
    }
}
