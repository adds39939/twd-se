using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Parsing;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class MichonneStep(ValidationContext context) : IValidationStep
{
    private static readonly string[] EventLogFileNames = ["_wdm_saveslot4_id.estore", "_wdm_saveslot4_id_Page971.epage"];

    public void Run()
    {
        context.Report.StepHeader("STEP 5: Validate Michonne EventLog format");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Michonne);
            if (files is { Count: > 0 })
                AddLuaFindings(details, files);
        }
        catch (Exception e)
        {
            details.Add($"Michonne archive error: {e.Message}");
        }

        var paths = EventLogFileNames.Select(name => context.TestSave("Michonne", name));
        var (_, dialogNodes) = EventLogFiles.Read(paths, details, reportMissing: true);
        context.MichonneNodeHashes = dialogNodes.Select(record => record.NodeHash).ToHashSet();

        if (dialogNodes.Count > 0)
            AddGuidMatches(details);
        else
            details.Add("ERROR: No dialog node records found in Michonne estore/epage!");

        context.Report.Add("5. Michonne EventLog format", dialogNodes.Count > 0, details);
    }

    private void AddLuaFindings(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (_, lua) = GameArchives.Find(files, "ChoiceStats");
        if (lua is not { IsEmpty: false })
        {
            details.Add("ChoiceStats.lua not found in Michonne archive");
            return;
        }

        var text = context.Archives.ReadLua(lua.Value).ToLowerInvariant();
        if (text.Contains("guid"))
            details.Add("CONFIRMED: Michonne ChoiceStats.lua references GUIDs");
        TextSearch.AddOccurrences(details, text, "executing dialog node", "dialog", "guid", "crc");
    }

    private void AddGuidMatches(List<string> details)
    {
        var matched = new List<string>();
        foreach (var (guid, (choice, value)) in MichonneGuids.All)
        {
            var braced = "{" + guid + "}";
            var hash = TelltaleCrc64.Compute(braced);
            if (context.MichonneNodeHashes.Contains(hash))
                matched.Add($"  {choice}={value}: CRC64(\"{braced}\") = 0x{hash:X16} FOUND");
        }

        details.Add($"\nMichonne GUID->CRC64 matches in real epage: {matched.Count}/{MichonneGuids.All.Count}");
        details.AddRange(matched.Take(10));

        if (matched.Count > 0)
            return;

        details.Add("NOTE: No mapped choice GUIDs found in this save (likely early-game autosave).");
        details.Add("The test save has dialog node events, but none correspond to major choice points.");
        details.Add("This is expected for saves before Episode 2/3 choices occur.");
        details.Add("GUID mapping correctness is verified by Step 9 (choice.prop cross-validation).");
    }
}
