using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractAllChoices.Model;

namespace TwdSaveEditor.Tools.ExtractAllChoices.Choices;

public static partial class ChoiceOrganizer
{
    private const int MaxThemeLength = 30;

    public static List<Question> Organize(List<Choice> choices)
    {
        var depths = choices.Select(choice => choice.Depth).Distinct().Order().ToList();
        if (depths.Count < 2)
        {
            return choices.Select(choice => new Question(choice.Text, choice.Guid, choice.Episode, choice.ExpressionId)).ToList();
        }

        var questions = MergeThemes(GroupByQuestion(choices, depths[0]));
        foreach (var question in questions)
        {
            question.Options = UniqueOptions(question.Options);
        }

        return questions;
    }

    private static List<Question> GroupByQuestion(List<Choice> choices, int questionDepth)
    {
        var organized = new List<Question>();
        Question? current = null;

        foreach (var choice in choices)
        {
            var text = TextFormat.Trim(choice.Text);
            if (text.Length == 0)
            {
                continue;
            }

            if (choice.Depth <= questionDepth + 1)
            {
                if (current != null)
                {
                    organized.Add(current);
                }

                current = new Question(text, choice.Guid, choice.Episode, choice.ExpressionId);
            }
            else
            {
                current ??= new Question("(Unknown question)", null, null, null);
                current.Options.Add(new Option(text, choice.Guid, choice.ExpressionId));
                if (choice.Episode is not (null or 0) && current.Episode is null or 0)
                {
                    current.Episode = choice.Episode;
                }
            }
        }

        if (current != null)
        {
            organized.Add(current);
        }

        return organized;
    }

    private static List<Question> MergeThemes(List<Question> organized)
    {
        var merged = new List<Question>();
        var index = 0;
        while (index < organized.Count)
        {
            var question = organized[index];
            if (index + 1 < organized.Count)
            {
                var next = organized[index + 1];
                var nextIsTheme = next.Text.Length < MaxThemeLength && IsUpper(next.Text);
                if (question.Options.Count == 0 && next.Options.Count > 0 && nextIsTheme)
                {
                    next.Text = $"{question.Text} [{next.Text}]";
                    merged.Add(next);
                    index += 2;
                    continue;
                }

                if (question.Options.Count > 0 && next.Options.Count == 0 && nextIsTheme)
                {
                    question.Text = $"{question.Text} [{next.Text}]";
                    merged.Add(question);
                    index += 2;
                    continue;
                }
            }

            merged.Add(question);
            index++;
        }

        return merged;
    }

    private static List<Option> UniqueOptions(List<Option> options)
    {
        var seen = new HashSet<string>();
        var unique = new List<Option>();
        foreach (var option in options)
        {
            var text = option.Text;
            var normalized = YouAndPrefix().Replace(text, "");
            normalized = PlayerShare().Replace(normalized, "");
            normalized = Percentage().Replace(normalized, "");
            normalized = TextFormat.Trim(normalized).ToLowerInvariant();

            if (seen.Contains(normalized))
            {
                continue;
            }

            if (text.StartsWith("of players", StringComparison.Ordinal))
            {
                continue;
            }

            if (!text.StartsWith("You", StringComparison.Ordinal) && !text.StartsWith("After", StringComparison.Ordinal))
            {
                var hasYouVersion = options.Any(other =>
                    other.Text.StartsWith("You", StringComparison.Ordinal) && CoreText(other.Text) == normalized);
                if (hasYouVersion)
                {
                    continue;
                }
            }

            var withoutComma = normalized.Replace(", ", " ");
            if (seen.Contains(withoutComma))
            {
                continue;
            }

            seen.Add(normalized);
            seen.Add(withoutComma);
            unique.Add(option);
        }

        return unique;
    }

    private static string CoreText(string text) => TextFormat.Trim(YouAndPlayerShare().Replace(text, "")).ToLowerInvariant();

    private static bool IsUpper(string text) => text.Any(char.IsUpper) && !text.Any(char.IsLower);

    [GeneratedRegex("^You and ")]
    private static partial Regex YouAndPrefix();

    [GeneratedRegex(@"^\d+\.?\d*%[\s\x1c-\x1f]*of players[\s\x1c-\x1f]*")]
    private static partial Regex PlayerShare();

    [GeneratedRegex(@"^\d+\.?\d*%[\s\x1c-\x1f]*")]
    private static partial Regex Percentage();

    [GeneratedRegex(@"^You and \d+\.?\d*%[\s\x1c-\x1f]*of players[\s\x1c-\x1f]*")]
    private static partial Regex YouAndPlayerShare();
}
