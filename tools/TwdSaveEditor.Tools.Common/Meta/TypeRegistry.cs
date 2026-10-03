namespace TwdSaveEditor.Tools.Common.Meta;

public sealed class TypeRegistry
{
    private static readonly string[] Intrinsics =
    [
        "String", "bool", "int", "int32", "long", "uint32", "unsignedint", "unsignedlong", "float", "double",
        "int64", "uint64", "__int64", "unsigned__int64", "int16", "uint16", "short", "unsignedshort",
        "int8", "uint8", "char", "unsignedchar", "Symbol", "Flags", "PropertySet", "ResourceBundle",
        "DCArray<String>", "Set<Symbol,less<Symbol>>", "Map<String,bool,less<String>>", "Map<String,String,less<String>>",
    ];

    private readonly Dictionary<ulong, string> _names = [];

    public TypeRegistry(ClassLayouts layouts)
    {
        foreach (var name in Intrinsics)
        {
            Add(name);
        }

        foreach (var name in layouts.TypeNames)
        {
            Add(name);
        }
    }

    public void Add(string name) => _names.TryAdd(TypeName.Hash(name), TypeName.Normalize(name));

    public string? Find(ulong hash) => _names.GetValueOrDefault(hash);
}
