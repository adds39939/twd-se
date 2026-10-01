namespace TwdSaveEditor.Core.Model;

public sealed class PropertySet
{
    public uint Version { get; set; } = 2;

    public uint Flags { get; set; }

    public List<Symbol> ParentSymbols { get; set; } = [];

    public List<TypeGroup> TypeGroups { get; set; } = [];

    public IEnumerable<Property> AllProperties => TypeGroups.SelectMany(g => g.Properties);
}
