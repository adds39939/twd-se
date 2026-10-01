using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractNodeMappings.EventLog;
using TwdSaveEditor.Tools.ExtractNodeMappings.Expressions;
using TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;
using TwdSaveEditor.Tools.ExtractNodeMappings.Model;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.Reporting;

public sealed partial class MappingReport(IReadOnlyDictionary<string, List<ChoiceMapping>> seasonMappings, EpageHashes epageHashes)
{
    private const int MaxQuestionLength = 60;
    private const int MaxOptionLength = 50;

    public string Build()
    {
        var lines = new List<string>();

        foreach (var (season, mappings) in SeasonsWithMappings())
            AddSeason(lines, season, mappings);

        lines.Add($"\n\n{new string('=', 80)}");
        lines.Add("COMPACT HASH MAPPING TABLE");
        lines.Add(new string('=', 80));
        lines.Add("Format: CRC64_Hex | Decimal | Question | Option");
        lines.Add(new string('-', 80));

        foreach (var (season, mappings) in SeasonsWithMappings())
            AddCompactTable(lines, season, mappings);

        return string.Join("\n", lines);
    }

    private IEnumerable<(string Season, List<ChoiceMapping> Mappings)> SeasonsWithMappings() =>
        Seasons.OutputOrder
            .Select(season => (season, seasonMappings.GetValueOrDefault(season, [])))
            .Where(entry => entry.Item2.Count > 0);

    private void AddSeason(List<string> lines, string season, List<ChoiceMapping> mappings)
    {
        lines.Add($"\n{new string('=', 70)}");
        lines.Add($"  {season}: {mappings.Count} choices");
        lines.Add(new string('=', 70));

        var useEpage = season == Seasons.Season3;
        var totalOptions = 0;
        var decimalOptions = 0;
        var guidOptions = 0;
        var emptyOptions = 0;
        var verifiedOptions = 0;

        foreach (var mapping in mappings)
        {
            lines.Add(FormatMapping(mapping, useEpage));
            lines.Add("");

            foreach (var option in mapping.Options)
            {
                totalOptions++;
                var expression = ExpressionParser.Parse(option.Expression);
                switch (expression.Type)
                {
                    case ExpressionKind.Decimal:
                        decimalOptions++;
                        if (useEpage && epageHashes.HasNode(expression.Decimal))
                            verifiedOptions++;
                        break;
                    case ExpressionKind.Guid:
                        guidOptions++;
                        break;
                    case ExpressionKind.Empty or ExpressionKind.Zero:
                        emptyOptions++;
                        break;
                }
            }
        }

        lines.Add($"\n  --- {season} Statistics ---");
        lines.Add($"  Total options: {totalOptions}");
        lines.Add($"  Decimal (CRC64 hash) options: {decimalOptions}");
        lines.Add($"  GUID options: {guidOptions}");
        lines.Add($"  Empty/zero options: {emptyOptions}");
        if (useEpage)
            lines.Add($"  Verified in epage files: {verifiedOptions}/{decimalOptions}");
    }

    private string FormatMapping(ChoiceMapping mapping, bool useEpage)
    {
        var lines = new List<string>
        {
            $"  Question: {Or(mapping.QuestionClean, mapping.Question)}",
            $"    Episode: {mapping.EpisodeCode} -> {mapping.TargetEpisode}",
            $"    Question Expression: {mapping.QuestionExpression} ({ExpressionParser.Parse(mapping.QuestionExpression).Type})",
            $"    Question Secondary:  {mapping.QuestionSecondary} ({ExpressionParser.Parse(mapping.QuestionSecondary).Type})",
        };

        for (var i = 0; i < mapping.Options.Count; i++)
        {
            var option = mapping.Options[i];
            var expression = ExpressionParser.Parse(option.Expression);
            var secondary = ExpressionParser.Parse(option.SecondaryExpression);

            lines.Add($"    Option {i + 1}: {Or(option.TextClean, option.Text)}");
            lines.Add($"      Expression: {option.Expression}");

            switch (expression.Type)
            {
                case ExpressionKind.Decimal:
                    lines.Add($"      -> CRC64 Hash: {expression.HexHash} (decimal: {expression.Decimal})");
                    if (useEpage)
                    {
                        lines.Add($"      -> In epage 'Executing Dialog Node': {(epageHashes.HasNode(expression.Decimal) ? "YES" : "NO")}");
                        if (epageHashes.HasChoice(expression.Decimal))
                            lines.Add("      -> In epage 'Dialog Choice': YES");
                    }

                    break;
                case ExpressionKind.Guid:
                    lines.Add($"      -> GUID: {expression.Guid}");
                    break;
                case ExpressionKind.CompoundGuid:
                    lines.Add($"      -> Compound GUIDs: {TextFormat.QuoteList(expression.Guids ?? [])}");
                    break;
            }

            lines.Add($"      Secondary Expr: {option.SecondaryExpression}");
            if (secondary.IsDecimal)
                lines.Add($"      -> Secondary CRC64: {secondary.HexHash}");

            if (option.Guid.Length > 0)
                lines.Add($"      GUID: {option.Guid}");
            if (option.Percentage.Length > 0)
                lines.Add($"      Percentage: {option.Percentage}%");
        }

        return string.Join("\n", lines);
    }

    private void AddCompactTable(List<string> lines, string season, List<ChoiceMapping> mappings)
    {
        lines.Add($"\n--- {season} ---");

        foreach (var mapping in mappings)
        {
            var question = Ellipsize(Or(mapping.QuestionClean, "(no question)"), MaxQuestionLength);

            foreach (var option in mapping.Options)
            {
                var expression = ExpressionParser.Parse(option.Expression);
                var optionText = Ellipsize(PlayerSharePrefix().Replace(Or(option.TextClean, "(no text)"), ""), MaxOptionLength);

                if (expression.IsDecimal)
                {
                    var inEpage = "";
                    if (season == Seasons.Season3)
                    {
                        inEpage = epageHashes.HasNode(expression.Decimal)
                            ? " [VERIFIED in save]"
                            : " [not in save - different choice or later episode]";
                    }

                    lines.Add($"  {expression.HexHash} | {expression.Decimal} | {question} | {optionText}{inEpage}");
                }
                else if (expression.Type == ExpressionKind.Guid)
                {
                    lines.Add($"  GUID:{expression.Guid} | - | {question} | {optionText}");
                }
            }
        }
    }

    private static string Or(string value, string fallback) => value.Length > 0 ? value : fallback;

    private static string Ellipsize(string text, int length) => text.Length > length ? text[..(length - 3)] + "..." : text;

    [GeneratedRegex(@"^You and \d+\.?\d*%[\s\x1c-\x1f]+of players[\s\x1c-\x1f]+")]
    private static partial Regex PlayerSharePrefix();
}
