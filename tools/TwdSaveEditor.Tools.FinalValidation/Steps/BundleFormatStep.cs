using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Bundles;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class BundleFormatStep(ValidationContext context) : IValidationStep
{
    private static readonly (string Season, string File)[] Bundles =
    [
        ("S1", "wd1_saveslot2.bundle"),
        ("S2", "wd2_saveslot1.bundle"),
        ("S3", "wd3_saveslot1.bundle"),
        ("S4", "wd4_saveslot1.bundle"),
        ("Michonne", "wdm_saveslot4.bundle"),
    ];

    public void Run()
    {
        context.Report.StepHeader("STEP 7: Validate bundle MetaStream format");
        var details = new List<string>();
        var passed = true;

        foreach (var (season, file) in Bundles)
        {
            var path = context.TestSave(season, file);
            if (!File.Exists(path))
            {
                details.Add($"{season}: file not found");
                continue;
            }

            if (MetaStreamParser.Parse(File.ReadAllBytes(path)) is not { } bundle)
            {
                passed = false;
                details.Add($"{season}: FAILED to parse MSV6!");
                continue;
            }

            AddBundle(details, season, file, bundle);
        }

        context.Report.Add("7. Bundle MetaStream format", passed, details);
    }

    private static void AddBundle(List<string> details, string season, string file, MetaStreamSections bundle)
    {
        details.Add($"\n{season} ({file}):");
        details.Add($"  Magic: {(bundle.Magic == MetaStreamParser.MagicMsv6 ? "MSV6" : $"0x{bundle.Magic:X8}")}");
        details.Add($"  Version entries: {bundle.VersionEntries.Count}");

        var table = BundleFileTable.Parse(bundle.Default);
        details.Add($"  Inner files ({table.Count}): {TextFormat.QuoteList(table.Select(entry => entry.Name))}");

        foreach (var entry in table.Where(entry => BundleFileTable.Contains(bundle, entry)))
        {
            if (BundleFileTable.ReadInnerFile(bundle, entry) is not { } inner)
            {
                details.Add($"  {entry.Name}: could not parse inner MetaStream");
            }
            else if (inner.Default.Length < 12)
            {
                details.Add($"  {entry.Name}: could not parse PropertySet");
            }
            else
            {
                details.Add($"  {entry.Name}: PS version={Bytes.U32(inner.Default, 0)}, flags=0x{Bytes.U32(inner.Default, 4):X8}");
            }
        }
    }
}
