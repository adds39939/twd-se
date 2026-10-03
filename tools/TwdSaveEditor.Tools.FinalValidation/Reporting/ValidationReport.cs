namespace TwdSaveEditor.Tools.FinalValidation.Reporting;

public sealed class ValidationReport(TextWriter output)
{
    private const int Width = 70;

    private readonly OrderedDictionary<string, bool> _results = [];

    public int FailedCount => _results.Values.Count(passed => !passed);

    public void StepHeader(string title)
    {
        output.WriteLine();
        output.WriteLine(new string('#', Width));
        output.WriteLine($"# {title}");
        output.WriteLine(new string('#', Width));
    }

    public void Add(string step, bool passed, List<string> details)
    {
        _results[step] = passed;
        output.WriteLine();
        output.WriteLine(new string('=', Width));
        output.WriteLine($"  [{Status(passed)}] {step}");
        output.WriteLine(new string('=', Width));

        if (details.Count == 0)
        {
            return;
        }

        foreach (var line in string.Join("\n", details).Split('\n'))
        {
            output.WriteLine($"  {line}");
        }
    }

    public void PrintSummary()
    {
        output.WriteLine();
        output.WriteLine(new string('=', Width));
        output.WriteLine("  FINAL SUMMARY");
        output.WriteLine(new string('=', Width));

        foreach (var (step, passed) in _results)
        {
            output.WriteLine($"  [{Status(passed)}] {step}");
        }

        var failed = FailedCount;
        output.WriteLine();
        output.WriteLine($"  Total: {_results.Count}  Passed: {_results.Count - failed}  Failed: {failed}");
        output.WriteLine();
        output.WriteLine(failed == 0
            ? "  ALL VALIDATIONS PASSED!"
            : $"  {failed} VALIDATION(S) FAILED - see details above");
    }

    private static string Status(bool passed) => passed ? "PASS" : "FAIL";
}
