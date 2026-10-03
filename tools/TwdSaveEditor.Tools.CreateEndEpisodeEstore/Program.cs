using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.CreateEndEpisodeEstore.Estores;

var saveDirectory = ToolPaths.Argument(args, 0, ToolPaths.SavesVariable);
var logName = args.Length > 1 ? args[1] : "_wd1_saveslot1_id";

if (saveDirectory == null)
{
    Console.Error.WriteLine("usage: TwdSaveEditor.Tools.CreateEndEpisodeEstore [save_directory] [log_name]");
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Pass the save directory or set {ToolPaths.SavesVariable}.");
    return 1;
}

var estore = EndEpisodeEstoreBuilder.Build(saveDirectory, logName, Console.Out);
if (estore == null)
{
    return 0;
}

var outputPath = Path.Combine(saveDirectory, logName + EndEpisodeEstoreBuilder.Extension);
File.WriteAllBytes(outputPath, estore);
Console.WriteLine($"Wrote {outputPath} ({estore.Length} bytes)");

Console.WriteLine(EndEpisodeEstoreBuilder.HasEndEpisodeEvent(estore)
    ? "Verified: End Episode event present"
    : "ERROR: End Episode event NOT found in output!");
return 0;
