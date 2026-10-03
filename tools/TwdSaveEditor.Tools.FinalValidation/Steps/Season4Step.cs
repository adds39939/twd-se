using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Bundles;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed partial class Season4Step(ValidationContext context) : IValidationStep
{
    private const string ChoiceStatsFile = "choicestats.pro";

    private static readonly string[] Keywords = ["choicestats.pro", "choicestats.prop"];

    public void Run()
    {
        context.Report.StepHeader("STEP 4: Validate S4 choicestats.pro format");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Season4);
            if (files is { Count: > 0 })
            {
                AddLuaFindings(details, files);
            }
        }
        catch (Exception e)
        {
            details.Add($"S4 archive error: {e.Message}");
        }

        context.Report.Add("4. S4 choicestats.pro format", ValidateSave(details), details);
    }

    private void AddLuaFindings(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (_, lua) = GameArchives.Find(files, "ChoiceStats");
        if (lua is not { IsEmpty: false })
        {
            details.Add("ChoiceStats.lua not found in S4 archive");
            var choiceFiles = files.Keys.Where(name => name.ToLowerInvariant().Contains("choice")).Take(10);
            details.Add($"Choice-related files: {TextFormat.QuoteList(choiceFiles)}");
            return;
        }

        var text = context.Archives.ReadLua(lua.Value);
        foreach (var keyword in Keywords)
        {
            if (!text.ToLowerInvariant().Contains(keyword))
            {
                continue;
            }

            details.Add($"CONFIRMED: S4 reads from '{keyword}'");
            details.AddRange(TextSearch.LinesContaining(text, keyword).Take(1).Select(line => $"  {TextSearch.Excerpt(line)}"));
        }
    }

    private bool ValidateSave(List<string> details)
    {
        var savePath = context.TestSave("S4", "wd4_saveslot1.bundle");
        if (!File.Exists(savePath))
        {
            details.Add($"ERROR: S4 test save not found at {savePath}");
            return false;
        }

        if (MetaStreamParser.Parse(File.ReadAllBytes(savePath)) is not { } bundle)
        {
            details.Add("ERROR: Could not parse S4 bundle!");
            return false;
        }

        var table = BundleFileTable.Parse(bundle.Default);
        details.Add($"\nS4 bundle inner files: {TextFormat.QuoteList(table.Select(entry => entry.Name))}");

        var hasChoiceStats = table.Any(entry => entry.Name == ChoiceStatsFile);
        details.Add($"Has choicestats.pro: {hasChoiceStats}");
        if (!hasChoiceStats)
        {
            details.Add("WARNING: choicestats.pro not found (save may be early-game)");
        }

        var passed = true;
        foreach (var entry in table.Where(entry => entry.Name == ChoiceStatsFile))
        {
            if (BundleFileTable.ReadInnerFile(bundle, entry) is { } inner && !ValidateChoiceStats(details, inner.Default))
            {
                passed = false;
            }
        }

        return passed;
    }

    private static bool ValidateChoiceStats(List<string> details, byte[] section)
    {
        if (section.Length >= 12)
        {
            details.Add($"PropertySet: version={Bytes.U32(section, 0)}, flags=0x{Bytes.U32(section, 4):X8}, data_size={Bytes.U32(section, 8)}");
        }

        var guids = AnyGuid().Matches(TextFormat.DecodeLatin1(section)).Select(match => match.Groups[1].Value).ToList();
        details.Add($"GUIDs found in choicestats.pro: {guids.Count}");
        details.Add($"Braced GUIDs '( {{GUID}} )' format: {BracedGuid().Matches(TextFormat.DecodeAscii(section)).Count}");

        var found = guids.Select(guid => guid.ToUpperInvariant()).ToHashSet();
        var matched = Season4Guids.All.Keys.Count(guid => found.Contains(guid.ToUpperInvariant()));
        details.Add($"Our S4 GUIDs found in real save: {matched}/{Season4Guids.All.Count}");
        if (matched == 0)
        {
            details.Add("ERROR: No S4 GUIDs matched!");
        }

        if (guids.Count > 0)
        {
            details.Add($"Sample GUIDs: {TextFormat.QuoteList(guids.Take(5))}");
        }

        return matched > 0;
    }

    [GeneratedRegex(@"\{?[ \t\n\r\f\v]*(" + TextSearch.GuidPattern + @")[ \t\n\r\f\v]*\}?")]
    private static partial Regex AnyGuid();

    [GeneratedRegex(@"\([\s\x1c-\x1f]*\{([0-9A-Fa-f-]{36})\}[\s\x1c-\x1f]*\)")]
    private static partial Regex BracedGuid();
}
