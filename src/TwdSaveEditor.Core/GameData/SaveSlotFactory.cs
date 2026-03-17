using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Creates new blank SaveSlot instances with valid bundle structure.
/// All structural constants (magic values, version entries, file hashes) are
/// extracted from real Definitive Edition save files.
/// </summary>
public static class SaveSlotFactory
{
    // Outer MetaStream version entries (from real saves)
    private static readonly VersionEntry[] OuterVersionEntries =
    [
        new(0xE09B099B8076C147, 0x5A585C97),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    // Inner MetaStream version entries (same for both metadata_slot.p and choices.prop)
    private static readonly VersionEntry[] InnerVersionEntries =
    [
        new(0xCD75DC4F6B9F15D2, 0x21F2BCC9),
        new(0x84283CB979D71641, 0x0527D6BF),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    // File table hashes (from real saves)
    private const ulong MetadataHash1 = 0xEE382691929657D5;
    private const ulong ChoicesHash1 = 0x819F96241D349414;
    private const ulong ChoiceStatsHash1 = 0xBD8881F09F440467;
    private const ulong CommonHash2 = 0xCD75DC4F6B9F15D2;

    /// <summary>
    /// Create a blank S1/S2 save slot with metadata_slot.p + choices.prop.
    /// </summary>
    public static SaveSlot CreateBlankS1S2(string fileName, string episodeId = "WalkingDead101")
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);
        var choices = CreateBlankChoices();

        // Build raw inner MetaStream files so BundleWriter can work
        var psWriter = new Binary.PropertySetWriter();
        var rawMetadata = BuildInnerMetaStream(psWriter.Write(metadata));
        var rawChoices = BuildInnerMetaStream(psWriter.Write(choices));

        var fileTable = new List<BundleFileEntry>
        {
            new()
            {
                Name = "metadata_slot.p",
                Offset = 0,
                Size = (uint)rawMetadata.Length,
                Hash1 = MetadataHash1,
                Hash2 = CommonHash2,
            },
            new()
            {
                Name = "choices.prop",
                Offset = (uint)rawMetadata.Length,
                Size = (uint)rawChoices.Length,
                Hash1 = ChoicesHash1,
                Hash2 = CommonHash2,
            },
        };

        return new SaveSlot
        {
            FilePath = fileName,
            FileName = fileName,
            OuterHeader = new MetaStreamHeader
            {
                Magic = MetaStreamHeader.MagicMsv6,
                VersionEntries = [.. OuterVersionEntries],
            },
            FileTable = fileTable,
            Metadata = metadata,
            Choices = choices,
            RawMetadataFile = rawMetadata,
            RawChoicesFile = rawChoices,
            RawInnerFiles = new Dictionary<string, byte[]>
            {
                ["metadata_slot.p"] = rawMetadata,
                ["choices.prop"] = rawChoices,
            },
        };
    }

    /// <summary>
    /// Create a blank S3/Michonne save slot with only metadata_slot.p (no choices file).
    /// S3 and Michonne store choices in external estore/epage EventLog files.
    /// </summary>
    public static SaveSlot CreateBlankS3Michonne(string fileName, string episodeId)
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);

        var psWriter = new Binary.PropertySetWriter();
        var rawMetadata = BuildInnerMetaStream(psWriter.Write(metadata));

        var fileTable = new List<BundleFileEntry>
        {
            new()
            {
                Name = "metadata_slot.p",
                Offset = 0,
                Size = (uint)rawMetadata.Length,
                Hash1 = MetadataHash1,
                Hash2 = CommonHash2,
            },
        };

        return new SaveSlot
        {
            FilePath = fileName,
            FileName = fileName,
            OuterHeader = new MetaStreamHeader
            {
                Magic = MetaStreamHeader.MagicMsv6,
                VersionEntries = [.. OuterVersionEntries],
            },
            FileTable = fileTable,
            Metadata = metadata,
            RawMetadataFile = rawMetadata,
            RawInnerFiles = new Dictionary<string, byte[]>
            {
                ["metadata_slot.p"] = rawMetadata,
            },
        };
    }

    /// <summary>
    /// Create a blank S4 save slot with metadata_slot.p + choicestats.pro.
    /// S4 stores choices as GUIDs in choicestats.pro instead of choices.prop.
    /// </summary>
    public static SaveSlot CreateBlankS4(string fileName, string episodeId)
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);
        var choiceStats = CreateBlankChoiceStats();

        var psWriter = new Binary.PropertySetWriter();
        var rawMetadata = BuildInnerMetaStream(psWriter.Write(metadata));
        var rawChoiceStats = BuildInnerMetaStream(psWriter.Write(choiceStats));

        var fileTable = new List<BundleFileEntry>
        {
            new()
            {
                Name = "metadata_slot.p",
                Offset = 0,
                Size = (uint)rawMetadata.Length,
                Hash1 = MetadataHash1,
                Hash2 = CommonHash2,
            },
            new()
            {
                Name = "choicestats.pro",
                Offset = (uint)rawMetadata.Length,
                Size = (uint)rawChoiceStats.Length,
                Hash1 = ChoiceStatsHash1,
                Hash2 = CommonHash2,
            },
        };

        return new SaveSlot
        {
            FilePath = fileName,
            FileName = fileName,
            OuterHeader = new MetaStreamHeader
            {
                Magic = MetaStreamHeader.MagicMsv6,
                VersionEntries = [.. OuterVersionEntries],
            },
            FileTable = fileTable,
            Metadata = metadata,
            ChoiceStats = choiceStats,
            RawMetadataFile = rawMetadata,
            RawChoiceStatsFile = rawChoiceStats,
            RawInnerFiles = new Dictionary<string, byte[]>
            {
                ["metadata_slot.p"] = rawMetadata,
                ["choicestats.pro"] = rawChoiceStats,
            },
        };
    }

    /// <summary>
    /// Create a new blank save slot with valid structure, ready for the user to configure choices.
    /// Kept for backward compatibility; delegates to CreateBlankS1S2.
    /// </summary>
    public static SaveSlot CreateBlank(string fileName, string episodeId = "WalkingDead101")
        => CreateBlankS1S2(fileName, episodeId);

    /// <summary>
    /// Create a new save with all choices from a given season pre-populated with default values.
    /// Uses the provided registry to resolve the season handler.
    /// </summary>
    public static SaveSlot CreateForSeason(ISeasonRegistry registry, string seasonKey, int episode, string fileName)
    {
        var handler = registry.Get(seasonKey)
            ?? throw new ArgumentException($"Unknown season: {seasonKey}");
        var episodeId = handler.GetEpisodeId(episode);
        var slot = handler.CreateBlankSave(fileName, episodeId);
        handler.PopulateChoices(slot, episode);
        return slot;
    }


    public static PropertySet CreateBlankMetadata(string episodeId, string fileName)
    {
        // Metadata has: int32 group (chapter count, slot index), bool group, String group
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
                        // Chapter count and save slot index from real saves
                        new Property(new Symbol(0x7C725227A47FD1BA), new IntValue(1)),
                        new Property(new Symbol(0x94C245DACB1ADDC3), new IntValue(1)),
                    ]
                },
                new TypeGroup(boolSymbol)
                {
                    Properties =
                    [
                        new Property(new Symbol(0x4F8338150CC8BCD6), new BoolValue(true)),
                    ]
                },
                new TypeGroup(stringSymbol)
                {
                    Properties =
                    [
                        new Property(new Symbol(0xB218E7C003A67CE9), new StringValue(episodeId)),
                        new Property(new Symbol(0xF235E9FCE9562E01), new StringValue(
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
                        // Start with an empty choices array
                        new Property(
                            Symbol.FromString("choices"),
                            new RawBytesValue(
                                SaveAccessor.SerializeStringBoolArray([]),
                                typeSymbol)),
                    ]
                },
            ]
        };
    }

    public static PropertySet CreateBlankChoiceStats()
    {
        // S4 choicestats.pro: PropertySet with a single String property (empty GUID string)
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

    public static byte[] BuildInnerMetaStream(byte[] propData)
    {
        using var ms = new MemoryStream();
        using var writer = new Binary.BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(MetaStreamHeader.MagicMsv6);
        writer.WriteUInt32((uint)propData.Length); // default section size
        writer.WriteUInt32(0);                     // debug section size
        writer.WriteUInt32(0);                     // async section size
        writer.WriteUInt32((uint)InnerVersionEntries.Length);
        foreach (var ve in InnerVersionEntries)
        {
            writer.WriteUInt64(ve.TypeCrc);
            writer.WriteUInt32(ve.VersionCrc);
        }
        writer.WriteBytes(propData);

        writer.Flush();
        return ms.ToArray();
    }

}
