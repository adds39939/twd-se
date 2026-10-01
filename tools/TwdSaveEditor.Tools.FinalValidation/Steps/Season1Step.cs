using TwdSaveEditor.Tools.Common.Bundles;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Parsing;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class Season1Step(ValidationContext context) : IValidationStep
{
    private const string Step = "1. S1 choices.prop format";
    private const string ChoicesFile = "choices.prop";

    public void Run()
    {
        context.Report.StepHeader("STEP 1: Validate S1 choices.prop format");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Season1);
            if (files == null)
            {
                details.Add("ERROR: Could not extract S1 archive");
                context.Report.Add(Step, false, details);
                return;
            }

            AddLuaReferences(details, files);
        }
        catch (Exception e)
        {
            details.Add($"Archive extraction error: {e.Message}");
        }

        context.Report.Add(Step, ValidateSave(details), details);
    }

    private void AddLuaReferences(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (_, lua) = GameArchives.Find(files, "SaveLoad.lua");
        if (lua is not { IsEmpty: false })
        {
            details.Add("SaveLoad.lua not found in S1 archive (may be named differently)");
            details.Add($"Available Lua files: {TextFormat.QuoteList(GameArchives.LuaFiles(files))}");
            return;
        }

        var text = context.Archives.ReadLua(lua.Value);
        var references = TextSearch.LinesContaining(text, "choices", "prop").Select(TextFormat.Trim).ToList();
        if (references.Count > 0)
        {
            details.Add($"Found {references.Count} 'choices.prop' references in SaveLoad.lua:");
            details.AddRange(references.Take(5).Select(reference => $"  {TextFormat.Truncate(reference, 120)}"));
            return;
        }

        details.Add("No 'choices.prop' references found in SaveLoad.lua (may use different file)");
        references = TextSearch.LinesContaining(text, "choice").Select(TextFormat.Trim).ToList();
        if (references.Count > 0)
        {
            details.Add($"Found {references.Count} 'choice' references:");
            details.AddRange(references.Take(5).Select(reference => $"  {TextFormat.Truncate(reference, 120)}"));
        }
    }

    private bool ValidateSave(List<string> details)
    {
        var savePath = context.TestSave("S1", "wd1_saveslot2.bundle");
        if (!File.Exists(savePath))
        {
            details.Add($"ERROR: S1 test save not found at {savePath}");
            return false;
        }

        if (MetaStreamParser.Parse(File.ReadAllBytes(savePath)) is not { } bundle)
        {
            details.Add("ERROR: Could not parse S1 bundle MSV6 header!");
            return false;
        }

        details.Add($"\nS1 bundle: magic=0x{bundle.Magic:X8}, {bundle.VersionEntries.Count} version entries");
        var table = BundleFileTable.Parse(bundle.Default);
        details.Add($"Inner files: {TextFormat.QuoteList(table.Select(entry => entry.Name))}");

        var passed = table.Any(entry => entry.Name == ChoicesFile);
        details.Add($"Has choices.prop: {passed}");
        if (!passed)
            details.Add("ERROR: choices.prop not found in S1 bundle!");

        foreach (var entry in table.Where(entry => entry.Name == ChoicesFile && BundleFileTable.Contains(bundle, entry)))
        {
            if (BundleFileTable.ReadInnerFile(bundle, entry) is not { } inner)
            {
                details.Add("Could not parse inner MetaStream for choices.prop");
                continue;
            }

            var choices = ChoicesContainerScanner.Scan(inner.Default);
            details.Add($"ChoicesContainer entries found: {choices.Count}");
            if (choices.Count > 0)
            {
                details.Add("Sample entries:");
                details.AddRange(choices.Take(5).Select(choice => $"  '{choice}'"));
            }
            else
            {
                passed = false;
                details.Add("ERROR: No ChoicesContainer entries found!");
            }
        }

        return passed;
    }
}
