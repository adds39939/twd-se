using System.Globalization;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Props;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Meta;

public sealed class MetaReader(ClassLayouts layouts, TypeRegistry types)
{
    public const string BundleType = "ResourceBundle";
    public const string PropertySetType = "PropertySet";

    private const uint EmbeddedParentFlag = 0x400;
    private const int BundleNameSize = 16;

    private static readonly HashSet<string> Unblocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool", "int", "int32", "long", "uint32", "unsignedint", "unsignedlong", "float", "double",
        "int64", "uint64", "__int64", "unsigned__int64", "int16", "uint16", "short", "unsignedshort",
        "int8", "uint8", "char", "unsignedchar", "Symbol", "Flags", "Vector2", "Vector3", "Vector4",
        "Quaternion", "Color", "Rect", "BoundingBox", "TRange<float>", "TRect<float>", "TRange<unsignedint>",
    };

    private Dictionary<ulong, uint> _versions = [];

    public TypeRegistry Types => types;

    public static MetaReader CreateDefault()
    {
        var layouts = ClassLayouts.LoadDefault();
        return new MetaReader(layouts, new TypeRegistry(layouts));
    }

    public MetaPropertySet? ReadPropertySet(ReadOnlySpan<byte> data) => Read(data, PropertySetType)?.Root as MetaPropertySet;

    public MetaDocument? Read(ReadOnlySpan<byte> data, string rootType)
    {
        var sections = MetaStreamParser.Parse(data);
        if (sections == null)
            return null;

        var previous = _versions;
        _versions = sections.VersionEntries.ToDictionary(entry => entry.Type, entry => entry.Version);
        var reader = new PropReader(sections.Default);
        try
        {
            var root = rootType == BundleType ? ReadBundle(reader, sections.Async) : ReadValue(rootType, reader);
            return new MetaDocument(sections, root, null, 0, (int)reader.Remaining);
        }
        catch (Exception e) when (e is MetaFormatException or ArgumentOutOfRangeException or IndexOutOfRangeException or OverflowException)
        {
            return new MetaDocument(sections, null, e.Message, reader.Position, (int)reader.Remaining);
        }
        finally
        {
            _versions = previous;
        }
    }

    private MetaBundle ReadBundle(PropReader reader, byte[] content)
    {
        var version = reader.I32();
        var count = reader.U32();
        CheckCount(count, reader);

        var entries = new List<(string Name, uint Offset, uint Size, ulong NameSymbol, ulong TypeSymbol)>();
        for (uint i = 0; i < count; i++)
        {
            var offset = reader.U32();
            var size = reader.U32();
            var field = reader.Read(BundleNameSize);
            var end = field.IndexOf((byte)0);
            var name = TextFormat.DecodeAscii(end < 0 ? field : field[..end]);
            entries.Add((name, offset, size, reader.U64(), reader.U64()));
        }

        reader.Position += reader.Remaining;

        var files = new List<MetaBundleFile>();
        foreach (var entry in entries)
        {
            var type = types.Find(entry.TypeSymbol);
            var inside = (long)entry.Offset + entry.Size <= content.Length;
            var document = inside && type != null ? Read(content.AsSpan((int)entry.Offset, (int)entry.Size), type) : null;
            files.Add(new MetaBundleFile(entry.Name, entry.NameSymbol, entry.TypeSymbol, type, entry.Offset, entry.Size, document));
        }

        return new MetaBundle(version, files);
    }

    private MetaNode ReadValue(string type, PropReader reader)
    {
        if (TryReadScalar(type, reader) is { } scalar)
            return scalar;

        if (type == PropertySetType)
            return ReadPropertySetBody(reader);

        var (template, arguments) = TypeName.Split(type);
        switch (template)
        {
            case "DCArray" or "List" or "Set" or "Deque" or "DArray" or "LinkedList" or "Queue":
                return ReadSequence(arguments[0], reader.U32(), reader);
            case "SArray":
                return ReadSequence(arguments[0], uint.Parse(arguments[1], CultureInfo.InvariantCulture), reader);
            case "Map":
                return ReadMap(arguments[0], arguments[1], reader);
        }

        return ReadClass(type, reader);
    }

    private static MetaNode? TryReadScalar(string type, PropReader reader)
    {
        switch (type)
        {
            case "String":
                return new MetaScalar(TextFormat.DecodeLatin1(reader.Read(reader.U32())));
            case "bool":
                return reader.U8() switch
                {
                    0x31 => new MetaScalar(true),
                    0x30 => new MetaScalar(false),
                    var other => throw new MetaFormatException($"invalid bool byte {other:X2}"),
                };
            case "int" or "int32" or "long":
                return new MetaScalar(reader.I32());
            case "uint32" or "unsignedint" or "unsignedlong" or "Flags":
                return new MetaScalar(reader.U32());
            case "float":
                return new MetaScalar(BitConverter.Int32BitsToSingle(reader.I32()));
            case "double":
                return new MetaScalar(BitConverter.Int64BitsToDouble((long)reader.U64()));
            case "int64" or "__int64":
                return new MetaScalar((long)reader.U64());
            case "uint64" or "unsigned__int64":
                return new MetaScalar(reader.U64());
            case "int16" or "short":
                return new MetaScalar((short)(reader.U8() | reader.U8() << 8));
            case "uint16" or "unsignedshort":
                return new MetaScalar((ushort)(reader.U8() | reader.U8() << 8));
            case "int8" or "uint8" or "char" or "unsignedchar":
                return new MetaScalar(reader.U8());
            case "Symbol":
                return new MetaSymbol(reader.U64(), false);
        }

        if (type.StartsWith("Handle<", StringComparison.Ordinal) || type is "HandleBase")
            return new MetaSymbol(reader.U64(), true);

        return null;
    }

    private MetaList ReadSequence(string elementType, uint count, PropReader reader)
    {
        CheckCount(count, reader);
        var items = new List<MetaNode>();
        for (uint i = 0; i < count; i++)
            items.Add(ReadValue(elementType, reader));

        return new MetaList(items);
    }

    private MetaMap ReadMap(string keyType, string valueType, PropReader reader)
    {
        var count = reader.U32();
        CheckCount(count, reader);
        var entries = new List<KeyValuePair<MetaNode, MetaNode>>();
        for (uint i = 0; i < count; i++)
        {
            var key = ReadValue(keyType, reader);
            entries.Add(KeyValuePair.Create(key, ReadValue(valueType, reader)));
        }

        return new MetaMap(entries);
    }

    private MetaObject ReadClass(string type, PropReader reader)
    {
        var layout = layouts.Find(type, _versions.GetValueOrDefault(TypeName.Hash(type), uint.MaxValue));
        if (layout == null || layout.Members.Count == 0)
            throw new MetaFormatException($"unknown type {type}");

        var members = new List<KeyValuePair<string, MetaNode>>();
        foreach (var member in layout.Members.Where(member => member.IsSerialized))
        {
            if (Unblocked.Contains(member.Type))
            {
                members.Add(KeyValuePair.Create(member.Name, ReadValue(member.Type, reader)));
                continue;
            }

            var start = reader.Position;
            var size = reader.U32();
            members.Add(KeyValuePair.Create(member.Name, ReadValue(member.Type, reader)));
            if (reader.Position != start + size)
                throw new MetaFormatException($"block of {type}.{member.Name} is {size} bytes, read {reader.Position - start}");
        }

        return new MetaObject(type, members);
    }

    private MetaPropertySet ReadPropertySetBody(PropReader reader)
    {
        var version = reader.I32();
        var flags = reader.U32();
        var start = reader.Position;
        var size = reader.U32();
        var end = start + size;

        var parentCount = reader.U32();
        CheckCount(parentCount, reader);
        var parents = new List<ulong>();
        for (uint i = 0; i < parentCount; i++)
            parents.Add(reader.U64());

        var properties = new List<MetaProperty>();
        string? error = null;

        var groupCount = reader.U32();
        CheckCount(groupCount, reader);
        for (uint g = 0; g < groupCount && error == null; g++)
        {
            var typeHash = reader.U64();
            var count = reader.U32();
            var type = types.Find(typeHash);
            if (type == null)
            {
                error = $"unknown type #{typeHash:X16} x{count}";
                break;
            }

            for (uint p = 0; p < count; p++)
            {
                var key = reader.U64();
                try
                {
                    properties.Add(new MetaProperty(key, type, ReadValue(type, reader)));
                }
                catch (MetaFormatException e)
                {
                    error = $"#{key:X16} <{type}>: {e.Message}";
                    break;
                }
            }
        }

        if (error == null && (flags & EmbeddedParentFlag) != 0 && reader.Position < end)
            properties.Add(new MetaProperty(0, PropertySetType, ReadPropertySetBody(reader)));

        if (error == null && reader.Position != end)
            error = $"property set ended at {reader.Position}, expected {end}";

        reader.Position = end;
        return new MetaPropertySet(version, flags, size, parents, properties, error);
    }

    private static void CheckCount(uint count, PropReader reader)
    {
        if (count > reader.Remaining)
            throw new MetaFormatException($"implausible count {count}");
    }
}
