using TwdSaveEditor.Tools.ExtractAllChoices.Model;

namespace TwdSaveEditor.Tools.ExtractAllChoices.Choices;

public static class ChoiceSummary
{
    public static string Build(IEnumerable<(string Season, List<Choice>? Choices)> seasons)
    {
        var lines = new List<string>();
        foreach (var (season, choices) in seasons)
        {
            lines.Add($"\n=== {season} ===");

            if (choices is not { Count: > 0 })
            {
                lines.Add("  (No choice data extracted - season may use a different format)");
                continue;
            }

            foreach (var question in ChoiceOrganizer.Organize(choices))
                AddQuestion(lines, question);
        }

        return string.Join("\n", lines);
    }

    private static void AddQuestion(List<string> lines, Question question)
    {
        var episode = question.Episode is { } number and not 0 ? $"Episode {number}" : "Episode ?";
        lines.Add($"{episode}: \"{question.Text}\"{Tag("Expr", question.ExpressionId)}");
        if (!string.IsNullOrEmpty(question.Guid))
            lines.Add($"  GUID={{{question.Guid}}}");

        foreach (var option in question.Options)
            lines.Add($"  Option: \"{option.Text}\"{Tag("GUID", option.Guid)}{Tag("Expr", option.ExpressionId)}");
    }

    private static string Tag(string label, string? value) => string.IsNullOrEmpty(value) ? "" : $" {label}={{{value}}}";
}
