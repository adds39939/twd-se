using System.Globalization;
using System.Text;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.DecodeEstore.Reporting;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var savesRoot = ToolPaths.Argument(args, 0, ToolPaths.SampleSavesVariable);
if (savesRoot == null)
{
    Console.Error.WriteLine("usage: TwdSaveEditor.Tools.DecodeEstore [sample_saves_directory]");
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Pass the directory holding the S3 and Michonne sample saves or set {ToolPaths.SampleSavesVariable}.");
    return 1;
}

new DecodeReport(savesRoot, Console.Out).Run();
return 0;
