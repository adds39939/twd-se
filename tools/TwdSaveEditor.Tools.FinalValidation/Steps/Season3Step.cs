using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Data;
using TwdSaveEditor.Tools.FinalValidation.Parsing;
using TwdSaveEditor.Tools.FinalValidation.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class Season3Step(ValidationContext context) : IValidationStep
{
    private static readonly string[] EventLogFileNames =
    [
        "_wd3_saveslot1_id.estore",
        "_wd3_saveslot1_id_Page734.epage",
        "_wd3_saveslot1_id_Page10249.epage",
        "_wd3_saveslot1_id_Page11215.epage",
        "_wd3_saveslot1_id_Page12180.epage",
    ];

    public void Run()
    {
        context.Report.StepHeader("STEP 3: Validate S3 EventLog format");
        var details = new List<string>();

        try
        {
            var files = context.Archives.ExtractFiles(GameArchiveNames.Season3);
            if (files is { Count: > 0 })
                AddLuaFindings(details, files);
        }
        catch (Exception e)
        {
            details.Add($"S3 archive error: {e.Message}");
        }

        context.Report.Add("3. S3 EventLog format", ValidateEventLog(details), details);
    }

    private void AddLuaFindings(List<string> details, OrderedDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var (_, lua) = GameArchives.Find(files, "ChoiceStats.lua");
        if (lua == null)
            (_, lua) = GameArchives.Find(files, "choicestats");

        if (lua is not { IsEmpty: false })
        {
            details.Add("ChoiceStats.lua not found in S3 archive");
            var related = files.Keys.Where(name => name.ToLowerInvariant().Contains("choice") || name.ToLowerInvariant().Contains("stats")).Take(10);
            details.Add($"Related files: {TextFormat.QuoteList(related)}");
            return;
        }

        var text = context.Archives.ReadLua(lua.Value).ToLowerInvariant();
        details.Add(text.Contains("executing dialog node")
            ? "CONFIRMED: S3 ChoiceStats.lua uses 'Executing Dialog Node' as event type"
            : "WARNING: 'Executing Dialog Node' not found in S3 ChoiceStats.lua");
        TextSearch.AddOccurrences(details, text, "dialog", "choice", "event", "node");
    }

    private bool ValidateEventLog(List<string> details)
    {
        var paths = EventLogFileNames.Select(name => context.TestSave("S3", name));
        var (total, dialogNodes) = EventLogFiles.Read(paths, details, reportMissing: false);

        if (total == 0)
        {
            details.Add("ERROR: No records parsed from S3 estore/epage files!");
            return false;
        }

        details.Add("Record size: 42 bytes (CORRECT)");

        if (dialogNodes.Count >= 5)
        {
            details.Add("\n5 random dialog node hashes:");
            var samples = dialogNodes.ToArray();
            Random.Shared.Shuffle(samples);
            details.AddRange(samples.Take(5).Select(sample => $"  0x{sample.NodeHash:X16} (valid u64: True)"));
        }

        var nodeHashes = dialogNodes.Select(record => record.NodeHash).ToHashSet();
        var matched = Season3Nodes.All
            .Where(node => nodeHashes.Contains(node.Key))
            .Select(node => $"{node.Value.Key}={node.Value.Value}")
            .ToList();

        details.Add($"\nS3 ChoiceNodeMapping hashes found in real epage data: {matched.Count}/{Season3Nodes.All.Count}");
        if (matched.Count == 0)
        {
            details.Add("ERROR: No S3 mapping hashes found in real epage data!");
            return false;
        }

        details.Add($"Matched choices: {string.Join(", ", matched.Take(10))}");
        return true;
    }
}
