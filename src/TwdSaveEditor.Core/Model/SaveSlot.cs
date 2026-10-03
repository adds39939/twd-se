using TwdSaveEditor.Core.Constants;

namespace TwdSaveEditor.Core.Model;

public sealed class SaveSlot
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }

    public required MetaStreamHeader OuterHeader { get; init; }

    public required List<BundleFileEntry> Files { get; init; }

    public PropertySet? Metadata =>
        (FindFile(BundleFileNames.SlotMetadata) ?? FindFile(BundleFileNames.SaveMetadata))?.Properties;

    public PropertySet? Choices => FindFile(ChoicesFileName)?.Properties;

    public PropertySet? ChoiceStats => FindFile(BundleFileNames.ChoiceStats)?.Properties;

    public string ChoicesFileName =>
        FindFile(BundleFileNames.Season1Choices) != null ? BundleFileNames.Season1Choices : BundleFileNames.Choices;

    public SaveSlot? Autosave { get; set; }

    public bool AutosaveDamaged { get; set; }

    public List<string> ObsoleteFileNames { get; } = [];

    public EventLog? EventLog { get; set; }

    public bool EventLogDamaged { get; set; }

    public List<SaveSlot> Checkpoints { get; } = [];

    public bool Modified { get; set; }

    public string? DetectedSeasonKey { get; set; }

    public BundleFileEntry? FindFile(string fileName) => Files.FirstOrDefault(file => file.IsNamed(fileName));

    public BundleFileEntry? FindFile(ulong nameSymbol) => Files.FirstOrDefault(file => file.NameSymbol == nameSymbol);
}
