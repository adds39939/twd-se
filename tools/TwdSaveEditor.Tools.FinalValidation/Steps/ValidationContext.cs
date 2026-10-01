using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Reporting;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class ValidationContext(GameArchives archives, ValidationReport report)
{
    public GameArchives Archives { get; } = archives;
    public ValidationReport Report { get; } = report;
    public HashSet<ulong> MichonneNodeHashes { get; set; } = [];

    public string TestSave(string season, string fileName) => Path.Combine(ToolPaths.TestData, season, fileName);
}
