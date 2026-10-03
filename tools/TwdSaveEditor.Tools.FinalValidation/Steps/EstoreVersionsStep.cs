using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.FinalValidation.Data;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class EstoreVersionsStep(ValidationContext context) : IValidationStep
{
    private static readonly (string Season, string File)[] Estores =
    [
        ("S3", "_wd3_saveslot1_id.estore"),
        ("Michonne", "_wdm_saveslot4_id.estore"),
    ];

    private static readonly (string Season, string File)[] Epages =
    [
        ("S3", "_wd3_saveslot1_id_Page734.epage"),
        ("Michonne", "_wdm_saveslot4_id_Page971.epage"),
    ];

    private static readonly VersionEntry[] Created = EstoreVersions.Created;

    public void Run()
    {
        context.Report.StepHeader("STEP 6: Validate estore version entries");
        var details = new List<string>();

        foreach (var (season, file) in Estores)
        {
            AddEstore(details, season, context.TestSave(season, file));
        }

        foreach (var (season, file) in Epages)
        {
            AddEpage(details, season, context.TestSave(season, file));
        }

        context.Report.Add("6. Estore version entries", true, details);
    }

    private static void AddEstore(List<string> details, string season, string path)
    {
        if (!File.Exists(path))
        {
            details.Add($"{season} estore not found");
            return;
        }

        if (MetaStreamParser.Parse(File.ReadAllBytes(path)) is not { } parsed)
        {
            details.Add($"{season} estore: failed to parse MSV6");
            return;
        }

        var real = parsed.VersionEntries;
        details.Add($"\n{season} estore: {real.Count} version entries");
        details.AddRange(real.Select(Describe));

        details.Add($"\nEStoreCreator produces {Created.Length} entries:");
        details.AddRange(Created.Select(Describe));

        if (real.Count != Created.Length)
        {
            details.Add($"WARNING: Count mismatch: real={real.Count} vs creator={Created.Length}");
        }

        var matching = MatchingCount(real);
        details.Add($"Matching entries: {matching}");
        if (matching == Created.Length)
        {
            details.Add("All EStoreCreator entries match real estore!");
            return;
        }

        AddDifference(details, "Missing from real", Created.Distinct().Where(entry => !real.Contains(entry)).ToList());
        AddDifference(details, "Extra in real", real.Distinct().Where(entry => !Created.Contains(entry)).ToList());
    }

    private static void AddDifference(List<string> details, string label, List<VersionEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        details.Add($"{label}: {entries.Count}");
        details.AddRange(entries.Select(entry => $"  0x{entry.Type:X16}, 0x{entry.Version:X8}"));
    }

    private static void AddEpage(List<string> details, string season, string path)
    {
        if (!File.Exists(path) || MetaStreamParser.Parse(File.ReadAllBytes(path)) is not { } parsed)
        {
            return;
        }

        var real = parsed.VersionEntries;
        details.Add($"\n{season} epage: {real.Count} version entries");
        details.Add($"  Matching EStoreCreator entries: {MatchingCount(real)}/{Created.Length}");
    }

    private static int MatchingCount(List<VersionEntry> real) => Created.Distinct().Count(real.Contains);

    private static string Describe(VersionEntry entry) => $"  TypeCrc=0x{entry.Type:X16}, VersionCrc=0x{entry.Version:X8}";
}
