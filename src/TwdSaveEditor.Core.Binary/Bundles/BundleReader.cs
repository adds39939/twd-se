using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class BundleReader
{
    private const uint MaxFiles = 1_000_000;

    private static readonly string[] PropertyFiles =
    [
        BundleFileNames.SlotMetadata,
        BundleFileNames.SaveMetadata,
        BundleFileNames.Choices,
        BundleFileNames.Season1Choices,
        BundleFileNames.ChoiceStats,
    ];

    public static SaveSlot Read(string filePath) => Read(File.ReadAllBytes(filePath), filePath);

    public static SaveSlot Read(byte[] data, string filePath)
    {
        var content = MetaStreamCodec.Read(data);
        var files = ReadFiles(content.Default, content.Async);

        foreach (var file in files.Where(file => PropertyFiles.Any(file.IsNamed)))
        {
            TryParseProperties(file);
        }

        return new SaveSlot
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            OuterHeader = content.Header,
            Files = files,
        };
    }

    public static bool TryParseProperties(BundleFileEntry file)
    {
        if (file.Properties != null)
        {
            return true;
        }

        try
        {
            file.Properties = PropertyFileCodec.Read(file.Data);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return false;
        }
    }

    private static List<BundleFileEntry> ReadFiles(byte[] table, byte[] content)
    {
        var files = new List<BundleFileEntry>();
        if (table.Length < 8)
        {
            return files;
        }

        using var stream = new MemoryStream(table);
        using var reader = new BinaryReaderEx(stream);

        reader.ReadUInt32();
        var fileCount = reader.ReadUInt32();
        if (fileCount > MaxFiles || fileCount * (long)BundleWriter.EntrySize > reader.Remaining)
        {
            throw new InvalidDataException($"Bundle file table is invalid ({fileCount} files).");
        }

        for (uint i = 0; i < fileCount; i++)
        {
            var offset = reader.ReadUInt32();
            var size = reader.ReadUInt32();
            var nameField = reader.ReadBytes(BundleFileEntry.NameFieldSize);
            var nameSymbol = reader.ReadUInt64();
            var typeSymbol = reader.ReadUInt64();

            if ((long)offset + size > content.Length)
            {
                throw new InvalidDataException($"Bundle file {i} lies outside the bundle content.");
            }

            files.Add(new BundleFileEntry
            {
                NameField = nameField,
                NameSymbol = nameSymbol,
                TypeSymbol = typeSymbol,
                Data = content.AsSpan((int)offset, (int)size).ToArray(),
                OriginalOffset = offset,
            });
        }

        return files;
    }
}
