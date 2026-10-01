using TwdSaveEditor.Tools.Common.Bundles;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Parsing;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class Season2Step(ValidationContext context) : IValidationStep
{
    private static readonly string[] Keywords = ["season1.prop", "choices.prop"];

    public void Run()
    {
        context.Report.StepHeader("STEP 2: Validate S2 season1.prop format");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Season2);
            if (files is { Count: > 0 })
                AddLuaReferences(details, files);
        }
        catch (Exception e)
        {
            details.Add($"Archive extraction error: {e.Message}");
        }

        context.Report.Add("2. S2 season1.prop / choices format", ValidateSave(details), details);
    }

    private void AddLuaReferences(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (_, lua) = GameArchives.Find(files, "SaveLoad.lua");
        if (lua is not { IsEmpty: false })
        {
            details.Add("SaveLoad.lua not found in S2 archive");
            details.Add($"Available Lua files: {TextFormat.QuoteList(GameArchives.LuaFiles(files))}");
            return;
        }

        var text = context.Archives.ReadLua(lua.Value);
        foreach (var keyword in Keywords)
        {
            if (!text.ToLowerInvariant().Contains(keyword))
                continue;

            details.Add($"Found '{keyword}' reference in S2 SaveLoad.lua");
            details.AddRange(TextSearch.LinesContaining(text, keyword).Take(1).Select(line => $"  {TextSearch.Excerpt(line)}"));
        }
    }

    private bool ValidateSave(List<string> details)
    {
        var savePath = context.TestSave("S2", "wd2_saveslot1.bundle");
        if (!File.Exists(savePath))
        {
            details.Add($"ERROR: S2 test save not found at {savePath}");
            return false;
        }

        if (MetaStreamParser.Parse(File.ReadAllBytes(savePath)) is not { } bundle)
        {
            details.Add("ERROR: Could not parse S2 bundle!");
            return false;
        }

        var table = BundleFileTable.Parse(bundle.Default);
        details.Add($"\nS2 bundle inner files: {TextFormat.QuoteList(table.Select(entry => entry.Name))}");

        var hasSeason1 = table.Any(entry => entry.Name == "season1.prop");
        var hasChoices = table.Any(entry => entry.Name == "choices.prop");
        details.Add($"Has season1.prop: {hasSeason1}, Has choices.prop: {hasChoices}");

        var choicesFile = hasSeason1 ? "season1.prop" : hasChoices ? "choices.prop" : null;
        if (choicesFile == null)
        {
            details.Add("ERROR: Neither season1.prop nor choices.prop found!");
            return false;
        }

        foreach (var entry in table.Where(entry => entry.Name == choicesFile))
        {
            if (BundleFileTable.ReadInnerFile(bundle, entry) is not { } inner)
                continue;

            var choices = ChoicesContainerScanner.Scan(inner.Default);
            details.Add($"ChoicesContainer entries in {choicesFile}: {choices.Count}");
            if (choices.Count > 0)
            {
                details.Add("Sample entries:");
                details.AddRange(choices.Take(5).Select(choice => $"  '{choice}'"));
            }
            else
            {
                details.Add("WARNING: No ChoicesContainer entries (may be empty save)");
            }
        }

        return true;
    }
}
