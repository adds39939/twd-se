using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed partial class MichonneGuidsStep(ValidationContext context) : IValidationStep
{
    private const string Step = "9. Michonne GUID cross-validation";

    public void Run()
    {
        context.Report.StepHeader("STEP 9: Cross-validate Michonne GUIDs");
        var details = new List<string>();
        var passed = true;

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Michonne);
            if (files is not { Count: > 0 })
            {
                details.Add("ERROR: Could not extract Michonne archive");
                context.Report.Add(Step, false, details);
                return;
            }

            passed = ValidateChoiceProp(details, files);
        }
        catch (Exception e)
        {
            details.Add($"Error: {e.Message}");
        }

        context.Report.Add(Step, passed, details);
    }

    private bool ValidateChoiceProp(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (name, data) = GameArchives.Find(files, "choice.prop");
        if (data is not { IsEmpty: false })
        {
            details.Add("WARNING: choice.prop not found in Michonne archive");
            return true;
        }

        details.Add($"Found: {name} ({data.Value.Length} bytes)");

        var text = TextFormat.DecodeAscii(MetaStreamParser.Parse(data.Value.Span)?.Default ?? data.Value.Span);
        if (text.Length == 0)
            return true;

        var guids = Guid().Matches(text).Select(match => match.Value).ToList();
        details.Add($"GUIDs found in Michonne choice.prop: {guids.Count}");

        var inEpage = guids.Count(guid => context.MichonneNodeHashes.Contains(TelltaleCrc64.Compute("{" + guid.ToUpperInvariant() + "}")));
        details.Add($"GUIDs whose CRC64 appears in real epage: {inEpage}/{guids.Count}");

        var found = guids.Select(guid => guid.ToUpperInvariant()).ToHashSet();
        var matched = MichonneGuids.All.Keys.Count(guid => found.Contains(guid.ToUpperInvariant()));
        details.Add($"Our MichonneNodes GUIDs found in choice.prop: {matched}/{MichonneGuids.All.Count}");
        if (matched == 0)
            details.Add("ERROR: No Michonne mapping GUIDs found in choice.prop!");

        return matched > 0;
    }

    [GeneratedRegex(TextSearch.GuidPattern)]
    private static partial Regex Guid();
}
