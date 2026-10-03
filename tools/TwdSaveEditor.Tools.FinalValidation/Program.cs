using System.Text;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.FinalValidation.Archives;
using TwdSaveEditor.Tools.FinalValidation.Reporting;
using TwdSaveEditor.Tools.FinalValidation.Steps;

Console.OutputEncoding = Encoding.UTF8;

var key = KeyFile.Load();
var archivesDirectory = ToolPaths.Archives(args);

Console.WriteLine(new string('=', 70));
Console.WriteLine("  TWD SAVE EDITOR - FINAL COMPREHENSIVE VALIDATION");
Console.WriteLine(new string('=', 70));
Console.WriteLine();
Console.WriteLine($"Archives: {archivesDirectory}");
Console.WriteLine($"Test data: {ToolPaths.TestData}");
Console.WriteLine($"CRC64 check: 0x{TelltaleCrc64.Compute("Executing Dialog Node"):X16}");

var report = new ValidationReport(Console.Out);
var context = new ValidationContext(new GameArchives(archivesDirectory, new BlowfishV7(key)), report);

IValidationStep[] steps =
[
    new Crc64Step(context),
    new Season1Step(context),
    new Season2Step(context),
    new Season3Step(context),
    new Season4Step(context),
    new MichonneStep(context),
    new EstoreVersionsStep(context),
    new BundleFormatStep(context),
    new Season3ChoicePropStep(context),
    new MichonneGuidsStep(context),
];

foreach (var step in steps)
{
    step.Run();
}

report.PrintSummary();
return report.FailedCount == 0 ? 0 : 1;
