using TwdSaveEditor.Tools.EditSave.Editing;

if (args.Length < 2)
{
    Console.WriteLine("Usage: EditSave <save directory> <slot bundle name> [--new <episode>] [--out <directory>] [--slot <number>] [--restart <episode>]... [--chapter <episode> <chapter id>] [choice key=value]...");
    Console.WriteLine("Lists every decision of the save; with changes, applies them and writes the save's files.");
    Console.WriteLine("With --out the whole save is copied there first, so the source directory is left alone.");
    Console.WriteLine("With --slot (and --out) the copy is written as that slot number.");
    Console.WriteLine("With --new a missing slot bundle is created as a new save starting at that episode.");
    Console.WriteLine("With --chapter the save resumes from that chapter; an unknown chapter id lists the episode's chapters.");
    return 1;
}

var created = args.Skip(2).SkipWhile(arg => arg != "--new").Skip(1).FirstOrDefault();
var session = created != null && !File.Exists(Path.Combine(args[0], args[1]))
    ? SaveSession.Create(args[1], int.Parse(created))
    : SaveSession.Load(args[0], args[1]);
var output = args.Skip(2).SkipWhile(arg => arg != "--out").Skip(1).FirstOrDefault();
var restarts = args.Zip(args.Skip(1)).Where(pair => pair.First == "--restart").Select(pair => int.Parse(pair.Second)).ToList();
var chapter = args.Skip(2).SkipWhile(arg => arg != "--chapter").Skip(1).Take(2).ToList();
var slotNumber = args.Skip(2).SkipWhile(arg => arg != "--slot").Skip(1).FirstOrDefault();
var assignments = args.Skip(2).Where(arg => arg.Contains('=')).ToList();
var original = created != null ? [] : session.FileNames;
var accessor = session.Choices;
if (accessor == null)
{
    Console.Error.WriteLine("This save has no editable decisions.");
    return 1;
}

foreach (var episode in restarts)
    session.RestartFromEpisode(episode);

if (chapter.Count == 2 && !session.RestartFromChapter(int.Parse(chapter[0]), chapter[1]))
{
    foreach (var entry in session.Chapters(int.Parse(chapter[0])))
        Console.Error.WriteLine($"{entry.Id,-28} {entry.Group,-10} {entry.Title}");

    return 1;
}

foreach (var assignment in assignments)
{
    var separator = assignment.LastIndexOf('=');
    accessor.SetChoiceValue(assignment[..separator], assignment[(separator + 1)..]);
}

foreach (var choice in session.ChoiceList)
{
    var index = accessor.DetectCurrentChoice(choice);
    var value = accessor.GetChoiceValue(choice.ChoiceKey);
    Console.WriteLine($"{choice.SeasonKey,-11} {choice.Episode} {choice.ChoiceKey,-46} {value ?? "-",-34} {(index >= 0 ? choice.Options[index].Label : string.Empty)}");
}

if (assignments.Count == 0 && restarts.Count == 0 && chapter.Count == 0 && slotNumber == null && created == null)
    return 0;

var renamer = slotNumber != null && output != null ? new SlotRenamer(int.Parse(slotNumber)) : null;
string Target(string name) => renamer?.Rename(name) ?? name;

var directory = output ?? args[0];
Directory.CreateDirectory(directory);
if (output != null)
{
    foreach (var name in original.Except(session.Slot.ObsoleteFileNames))
        File.Copy(Path.Combine(args[0], name), Path.Combine(directory, Target(name)), true);
}

renamer?.Apply(session.Slot);
foreach (var file in session.Build())
{
    File.WriteAllBytes(Path.Combine(directory, Target(file.Name)), file.Data);
    Console.WriteLine($"wrote {Target(file.Name)} ({file.Data.Length} bytes)");
}

foreach (var name in output == null ? session.Slot.ObsoleteFileNames : [])
{
    File.Delete(Path.Combine(directory, name));
    Console.WriteLine($"removed {name}");
}

return 0;
