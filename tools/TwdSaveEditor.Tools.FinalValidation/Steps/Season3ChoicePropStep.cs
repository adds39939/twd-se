using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed partial class Season3ChoicePropStep(ValidationContext context) : IValidationStep
{
    private const string Step = "8. S3 choice.prop cross-validation";

    private static readonly string[] AlternativePatterns = ["choicestat", "choice_stat", "stats.prop"];

    public void Run()
    {
        context.Report.StepHeader("STEP 8: Cross-validate S3 choice.prop vs ChoiceNodeMapping");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Season3);
            if (files is not { Count: > 0 })
            {
                details.Add("ERROR: Could not extract S3 archive");
                context.Report.Add(Step, false, details);
                return;
            }

            AddChoiceProp(details, files);
        }
        catch (Exception e)
        {
            details.Add($"Error: {e.Message}");
            Console.Error.WriteLine(e);
        }

        context.Report.Add(Step, true, details);
    }

    private static void AddChoiceProp(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (name, data) = GameArchives.Find(files, "choice.prop");
        if (data == null)
        {
            details.Add("choice.prop not found in S3 archive");
            foreach (var pattern in AlternativePatterns)
            {
                var (alternativeName, alternative) = GameArchives.Find(files, pattern);
                if (alternative is not { IsEmpty: false })
                    continue;

                details.Add($"Found alternative: {alternativeName}");
                (name, data) = (alternativeName, alternative);
                break;
            }
        }

        if (data is not { IsEmpty: false })
        {
            details.Add("WARNING: No choice.prop file found in S3 archive");
            return;
        }

        details.Add($"Found: {name} ({data.Value.Length} bytes)");

        if (MetaStreamParser.Parse(data.Value.Span) is not { } inner)
        {
            details.Add("Not a standard MetaStream, trying raw parse");
            details.Add($"Decimal IDs in raw data: {DecimalId().Matches(TextFormat.DecodeAscii(data.Value.Span)).Count}");
            return;
        }

        details.Add($"Default section: {inner.Default.Length} bytes");

        var text = TextFormat.DecodeAscii(inner.Default);
        var decimalIds = DecimalId().Matches(text).Select(match => UInt128.Parse(match.Groups[1].Value)).ToList();
        details.Add($"Decimal expression IDs found: {decimalIds.Count}");

        var matching = decimalIds.Count(id => id <= ulong.MaxValue && Season3Nodes.All.ContainsKey((ulong)id));
        details.Add($"Expression IDs matching S3 ChoiceNodeMapping: {matching}");
        details.Add($"Expression IDs not in mapping: {decimalIds.Count - matching}");
        details.Add($"Total S3 mapping entries: {Season3Nodes.All.Count}");
        details.Add($"GUIDs found in S3 choice.prop: {Guid().Matches(text).Count}");
    }

    [GeneratedRegex(@"\{([0-9]{15,20})\}")]
    private static partial Regex DecimalId();

    [GeneratedRegex(TextSearch.GuidPattern)]
    private static partial Regex Guid();
}
