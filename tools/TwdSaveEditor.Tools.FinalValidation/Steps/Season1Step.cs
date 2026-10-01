using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.FinalValidation.Data;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class Season1Step(ValidationContext context) : IValidationStep
{
    private const string Step = "1. S1 persistent choice format";
    private const string KeyNamesFile = "persistent.prop";
    private const string SlotMetadataFile = "metadata_slot.prop";
    private const string TrackerFile = "choices.prop";
    private const int FirstEpisode = 101;
    private const int LastEpisode = 106;

    private readonly MetaReader _reader = MetaReader.CreateDefault();

    public void Run()
    {
        context.Report.StepHeader("STEP 1: Validate S1 persistent choice format");
        var details = new List<string>();
        var passed = false;

        try
        {
            var keys = ReadKeyNames(details);
            passed = keys != null && ValidateSave(details, keys);
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            details.Add($"ERROR: {e.Message}");
        }

        context.Report.Add(Step, passed, details);
    }

    private Dictionary<int, List<string>>? ReadKeyNames(List<string> details)
    {
        var files = context.Archives.ExtractFiles(GameArchiveNames.Season1);
        if (files == null || !files.TryGetValue(KeyNamesFile, out var data))
        {
            details.Add($"ERROR: Could not read {KeyNamesFile} from the S1 archive");
            return null;
        }

        var properties = _reader.ReadPropertySet(data.Span)
            ?? throw new InvalidDataException($"{KeyNamesFile} is not a property set");

        var keys = new Dictionary<int, List<string>>();
        for (var episode = FirstEpisode; episode <= LastEpisode; episode++)
        {
            if (properties.Find($"Persistent - {episode} - Key Names") is MetaList names)
                keys[episode] = names.Strings.ToList();
        }

        details.Add($"Game defines {keys.Values.Sum(names => names.Count)} persistent keys for episodes {string.Join(", ", keys.Keys)}");
        return keys;
    }

    private bool ValidateSave(List<string> details, Dictionary<int, List<string>> keys)
    {
        var savePath = context.TestSave("S1", "wd1_saveslot2.bundle");
        if (!File.Exists(savePath))
        {
            details.Add($"ERROR: S1 test save not found at {savePath}");
            return false;
        }

        if (_reader.Read(File.ReadAllBytes(savePath), MetaReader.BundleType)?.Root is not MetaBundle bundle)
        {
            details.Add("ERROR: Could not parse the S1 slot bundle");
            return false;
        }

        var metadata = FindProperties(bundle, SlotMetadataFile);
        var tracker = FindProperties(bundle, TrackerFile);
        if (metadata == null || tracker == null)
        {
            details.Add($"ERROR: {SlotMetadataFile} or {TrackerFile} missing from the slot bundle");
            return false;
        }

        var passed = true;
        foreach (var (episode, names) in keys)
        {
            var stored = names.Count(name => metadata.Find($"Persistent - {episode} - {name}") is MetaScalar { Text: not null });
            var trackerEntries = (tracker.Find($"Episode {episode}") as MetaMap)?.Entries.Count ?? 0;
            details.Add($"Episode {episode}: {stored}/{names.Count} persistent values in the slot, {trackerEntries} tracker entries");
            if (episode == FirstEpisode && stored != names.Count)
                passed = false;
        }

        if (!passed)
            details.Add("ERROR: The slot does not hold every persistent value of its first episode!");

        return passed;
    }

    private static MetaPropertySet? FindProperties(MetaBundle bundle, string fileName) =>
        bundle.Files.FirstOrDefault(file => file.NameSymbol == TypeName.Hash(fileName))?.Content?.Root as MetaPropertySet;
}
