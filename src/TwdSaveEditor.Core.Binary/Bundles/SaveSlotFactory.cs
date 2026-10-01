using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class SaveSlotFactory
{
    private static readonly VersionEntry[] OuterVersionEntries =
    [
        new(0xE09B099B8076C147, 0x5A585C97),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    private static readonly VersionEntry[] InnerVersionEntries =
    [
        new(0xCD75DC4F6B9F15D2, 0x21F2BCC9),
        new(0x84283CB979D71641, 0x0527D6BF),
        new(0x004F023463D89FB0, 0xB539B0FF),
    ];

    private const ulong MetadataHash1 = 0xEE382691929657D5;
    private const ulong ChoicesHash1 = 0x819F96241D349414;
    private const ulong Season1PropHash1 = 0xC6D68CC6611E12F9;
    private const ulong ChoiceStatsHash1 = 0xBD8881F09F440467;
    private const ulong CommonHash2 = 0xCD75DC4F6B9F15D2;

    public static SaveSlot CreateBlankWithChoices(string fileName, string episodeId = "WalkingDead101",
        string choicesFileName = "choices.prop")
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);
        var choices = CreateBlankChoices();

        var psWriter = new PropertySetWriter();
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
                Name = choicesFileName,
                Offset = (uint)rawMetadata.Length,
                Size = (uint)rawChoices.Length,
                Hash1 = choicesFileName == "season1.prop" ? Season1PropHash1 : ChoicesHash1,
                Hash2 = CommonHash2,
            },
        };

        return new SaveSlot
        {
            FilePath = fileName,
            FileName = fileName,
            ChoicesFileName = choicesFileName,
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
                [choicesFileName] = rawChoices,
            },
        };
    }

    public static SaveSlot CreateBlankMetadataOnly(string fileName, string episodeId)
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);

        var psWriter = new PropertySetWriter();
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

    public static SaveSlot CreateBlankWithChoiceStats(string fileName, string episodeId)
    {
        var metadata = CreateBlankMetadata(episodeId, fileName);
        var choiceStats = CreateBlankChoiceStats();

        var psWriter = new PropertySetWriter();
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

    public static byte[] BuildInnerMetaStream(byte[] propData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(MetaStreamHeader.MagicMsv6);
        writer.WriteUInt32((uint)propData.Length);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
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
