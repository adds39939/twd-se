using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class SaveSlotFactory
{
    private const uint ResourceBundleVersion = 0x5A585C97;
    private const uint SymbolVersion = 0xB539B0FF;

    public static SaveSlot Create(string fileName, params (string Name, PropertySet Properties)[] files) => new()
    {
        FilePath = fileName,
        FileName = fileName,
        OuterHeader = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries =
            [
                new VersionEntry(TelltaleTypes.ResourceBundle, ResourceBundleVersion),
                new VersionEntry(TelltaleTypes.Symbol, SymbolVersion),
            ],
        },
        Files = files.Select(file => CreateFile(file.Name, file.Properties)).ToList(),
    };

    public static BundleFileEntry CreateFile(string name, PropertySet properties)
    {
        var file = BundleFileEntry.Create(name, TelltaleTypes.PropertySet, []);
        file.Properties = properties;
        return file;
    }

    public static SaveSlot CreateBlankWithChoices(string fileName, string episodeId = "WalkingDead101",
        string choicesFileName = BundleFileNames.Choices) =>
        Create(fileName,
            (BundleFileNames.SlotMetadata, CreateBlankMetadata(episodeId, fileName)),
            (choicesFileName, CreateBlankChoices()));

    public static SaveSlot CreateBlankMetadataOnly(string fileName, string episodeId) =>
        Create(fileName, (BundleFileNames.SlotMetadata, CreateBlankMetadata(episodeId, fileName)));

    public static SaveSlot CreateBlankWithChoiceStats(string fileName, string episodeId) =>
        Create(fileName,
            (BundleFileNames.SlotMetadata, CreateBlankMetadata(episodeId, fileName)),
            (BundleFileNames.ChoiceStats, CreateBlankChoiceStats()));

    public static SaveSlot CreateBlank(string fileName, string episodeId = "WalkingDead101")
        => CreateBlankWithChoices(fileName, episodeId);

    public static PropertySet CreateBlankMetadata(string episodeId, string fileName)
    {
        var int32Symbol = Symbol.FromString("int32");
        var boolSymbol = Symbol.FromString("bool");
        var stringSymbol = Symbol.FromString("String");

        return new PropertySet
        {
            Version = 2,
            Flags = 0x100,
            TypeGroups =
            [
                new TypeGroup(int32Symbol)
                {
                    Properties =
                    [
                        new Property(Symbol.FromString(SlotMetadataKeys.LatestSerial), new IntValue(1)),
                        new Property(Symbol.FromString(SlotMetadataKeys.Progress), new IntValue(1)),
                    ]
                },
                new TypeGroup(boolSymbol)
                {
                    Properties =
                    [
                        new Property(Symbol.FromString(SlotMetadataKeys.CompletedEpisode(1)), new BoolValue(true)),
                    ]
                },
                new TypeGroup(stringSymbol)
                {
                    Properties =
                    [
                        new Property(Symbol.FromString(SlotMetadataKeys.EpisodeInProgress), new StringValue(episodeId)),
                        new Property(Symbol.FromString(SlotMetadataKeys.LatestSave), new StringValue(
                            "_" + Path.GetFileNameWithoutExtension(fileName) + "_autosave.bundle")),
                    ]
                },
            ]
        };
    }

    public static PropertySet CreateBlankChoices()
    {
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);

        return new PropertySet
        {
            Version = 2,
            Flags = 0x0,
            TypeGroups =
            [
                new TypeGroup(typeSymbol)
                {
                    Properties =
                    [
                        new Property(
                            Symbol.FromString("choices"),
                            new RawBytesValue(
                                ChoicesContainer.Serialize([]),
                                typeSymbol)),
                    ]
                },
            ]
        };
    }

    public static PropertySet CreateBlankChoiceStats()
    {
        var stringSymbol = new Symbol(TelltaleTypes.String);

        return new PropertySet
        {
            Version = 2,
            Flags = 0x100,
            TypeGroups =
            [
                new TypeGroup(stringSymbol)
                {
                    Properties =
                    [
                        new Property(new Symbol(0x82433F1B9ADB69DA), new StringValue("")),
                    ]
                },
            ]
        };
    }
}
