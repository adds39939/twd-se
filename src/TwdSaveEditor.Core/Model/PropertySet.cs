using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Model;

public sealed class PropertySet
{
    public uint Version { get; set; } = 2;

    public uint Flags { get; set; }

    public List<Symbol> ParentSymbols { get; set; } = [];

    public List<TypeGroup> TypeGroups { get; set; } = [];

    public IEnumerable<Property> AllProperties => TypeGroups.SelectMany(g => g.Properties);

    public Property? Find(Symbol key) => AllProperties.FirstOrDefault(property => property.KeySymbol == key);

    public Property? Find(string name) => Find(Symbol.FromString(name));

    public string? GetString(string name) => (Find(name)?.Value as StringValue)?.Value;

    public int? GetInt(string name) => (Find(name)?.Value as IntValue)?.Value;

    public bool? GetBool(string name) => (Find(name)?.Value as BoolValue)?.Value;

    public IReadOnlyList<string>? GetStrings(string name) => (Find(name)?.Value as StringArrayValue)?.Values;

    public void SetString(string name, string value) =>
        Set(Symbol.FromString(name), new Symbol(TelltaleTypes.String), new StringValue(value));

    public void SetInt(string name, int value) =>
        Set(Symbol.FromString(name), new Symbol(TelltaleTypes.Int32), new IntValue(value));

    public void SetBool(string name, bool value) =>
        Set(Symbol.FromString(name), new Symbol(TelltaleTypes.Bool), new BoolValue(value));

    public void SetStrings(string name, IEnumerable<string> values) =>
        Set(Symbol.FromString(name), new Symbol(TelltaleTypes.StringArray), new StringArrayValue([.. values]));

    public void Set(Symbol key, Symbol typeSymbol, PropertyValue value)
    {
        var group = TypeGroups.FirstOrDefault(g => g.TypeSymbol == typeSymbol);
        if (group?.Properties.FirstOrDefault(property => property.KeySymbol == key) is { } existing)
        {
            existing.Value = value;
            return;
        }

        Remove(key);

        if (group == null)
        {
            group = new TypeGroup(typeSymbol);
            TypeGroups.Insert(InsertIndex(TypeGroups, g => g.TypeSymbol, typeSymbol), group);
        }

        group.Properties.Insert(InsertIndex(group.Properties, property => property.KeySymbol, key), new Property(key, value));
    }

    public bool Remove(Symbol key)
    {
        foreach (var group in TypeGroups)
        {
            var index = group.Properties.FindIndex(property => property.KeySymbol == key);
            if (index < 0)
            {
                continue;
            }

            group.Properties.RemoveAt(index);
            if (group.Properties.Count == 0)
            {
                TypeGroups.Remove(group);
            }

            return true;
        }

        return false;
    }

    private static int InsertIndex<T>(List<T> items, Func<T, Symbol> symbol, Symbol value)
    {
        var index = items.FindIndex(item => symbol(item).Value > value.Value);
        return index < 0 ? items.Count : index;
    }
}
