using TwdSaveEditor.Tools.EditSave.Editing;

if (args.Length < 2)
{
    Console.WriteLine("Usage: EditSave <save directory> <slot bundle name> [--new <episode>] [--out <directory>] [--slot <number>] [--restart <episode>]... [--chapter <episode> <chapter id>] [--carried] [--give <item id>]... [--take <item id>]... [choice key=value]...");
    Console.WriteLine("Lists every decision of the save; with changes, applies them and writes the save's files.");
    Console.WriteLine("With --out the whole save is copied there first, so the source directory is left alone.");
    Console.WriteLine("With --slot (and --out) the copy is written as that slot number.");
    Console.WriteLine("With --new a missing slot bundle is created as a new save starting at that episode.");
    Console.WriteLine("With --chapter the save resumes from that chapter; an unknown chapter id lists the episode's chapters.");
    Console.WriteLine("With --carried the resume save gets the items picked up earlier in the episode; --give and --take change single items.");
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
var carried = args.Skip(2).Contains("--carried");
var give = args.Zip(args.Skip(1)).Where(pair => pair.First == "--give").Select(pair => pair.Second).ToList();
var take = args.Zip(args.Skip(1)).Where(pair => pair.First == "--take").Select(pair => pair.Second).ToList();
var inventoryChanged = carried || give.Count > 0 || take.Count > 0;
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

if (inventoryChanged && !session.ChangeInventory(carried, give, take))
{
    Console.Error.WriteLine(session.Inventory?.Unavailable ?? "This season has no editable inventory.");
    return 1;
}

foreach (var choice in session.ChoiceList)
{
    var index = accessor.DetectCurrentChoice(choice);
    var value = accessor.GetChoiceValue(choice.ChoiceKey);
    Console.WriteLine($"{choice.SeasonKey,-11} {choice.Episode} {choice.ChoiceKey,-46} {value ?? "-",-34} {(index >= 0 ? choice.Options[index].Label : string.Empty)}");
}

if (session.Inventory is { } inventory)
{
    Console.WriteLine(inventory.Editable
        ? $"{inventory.Owner}, episode {inventory.Episode}: {(inventory.Held.Count == 0 ? "no items" : string.Join(", ", inventory.Held))}"
        : inventory.Unavailable);
    foreach (var item in inventory.Items)
        Console.WriteLine($"  {(inventory.Held.Contains(item.Id) ? "x" : " ")} {item.Id,-28} {item.Name}");
}

if (assignments.Count == 0 && restarts.Count == 0 && chapter.Count == 0 && slotNumber == null && created == null && !inventoryChanged)
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
