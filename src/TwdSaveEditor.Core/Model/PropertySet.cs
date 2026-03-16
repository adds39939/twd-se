namespace TwdSaveEditor.Core.Model;

/// <summary>
/// A Telltale PropertySet — a collection of typed properties keyed by CRC64 symbols,
/// organized into type groups. Definitive Series uses version 2 with flags prefix.
/// </summary>
public sealed class PropertySet
{
    /// <summary>PropertySet format version (typically 2 for Definitive Series).</summary>
    public uint Version { get; set; } = 2;

    /// <summary>Flags field (0x100 for metadata, 0x0 for choices).</summary>
    public uint Flags { get; set; }

    public List<Symbol> ParentSymbols { get; set; } = [];

    /// <summary>
    /// Properties organized by type symbol. Each type group contains properties of that type.
    /// </summary>
    public List<TypeGroup> TypeGroups { get; set; } = [];

    /// <summary>
    /// Flat view of all properties across all type groups.
    /// </summary>
    public IEnumerable<Property> AllProperties => TypeGroups.SelectMany(g => g.Properties);
}

public sealed class TypeGroup(Symbol typeSymbol)
{
    public Symbol TypeSymbol { get; } = typeSymbol;
    public List<Property> Properties { get; set; } = [];
}
