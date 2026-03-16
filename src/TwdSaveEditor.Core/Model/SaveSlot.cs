namespace TwdSaveEditor.Core.Model;

/// <summary>
/// Represents a loaded .bundle save file from TWD: The Telltale Definitive Series.
/// Contains the outer MetaStream header, file table, and parsed inner files.
/// </summary>
public sealed class SaveSlot
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }

    /// <summary>The outer MetaStream header from the bundle file.</summary>
    public required MetaStreamHeader OuterHeader { get; init; }

    /// <summary>File table entries from the bundle's default section.</summary>
    public required List<BundleFileEntry> FileTable { get; init; }

    /// <summary>Parsed metadata PropertySet from metadata_slot.p.</summary>
    public PropertySet? Metadata { get; set; }

    /// <summary>Parsed choices PropertySet from choices.prop.</summary>
    public PropertySet? Choices { get; set; }

    /// <summary>Raw bytes of the entire bundle, kept for sections we don't modify.</summary>
    public byte[]? RawBundleData { get; init; }

    /// <summary>Raw inner MetaStream header+sections for metadata_slot.p.</summary>
    public byte[]? RawMetadataFile { get; init; }

    /// <summary>Raw inner MetaStream header+sections for choices.prop.</summary>
    public byte[]? RawChoicesFile { get; init; }

    /// <summary>
    /// All raw inner files by name, extracted from the (possibly decompressed) async section.
    /// Used for round-tripping files we don't modify.
    /// </summary>
    public Dictionary<string, byte[]>? RawInnerFiles { get; init; }
}

/// <summary>
/// An entry in the bundle file table, pointing to an inner MetaStream in the async section.
/// </summary>
public sealed class BundleFileEntry
{
    public required string Name { get; init; }
    public required uint Offset { get; init; }
    public required uint Size { get; init; }
    public required ulong Hash1 { get; init; }
    public required ulong Hash2 { get; init; }
}
