using System.Text.Json;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.PropertySets;

internal static class PropertyValueLayouts
{
    private const string ResourceSuffix = "property-value-layouts.json";
    private const string Block = "block";
    private const string Count = "count";
    private const string SymbolLayout = "symbol";

    private static readonly Dictionary<ulong, Func<BinaryReaderEx, Symbol, PropertyValue>> Readers = Load();

    public static PropertyValue Read(BinaryReaderEx reader, Symbol type)
    {
        if (!Readers.TryGetValue(type.Value, out var read))
        {
            throw new InvalidDataException($"Unknown property type 0x{type.Value:X16} at position {reader.Position}.");
        }

        return read(reader, type);
    }

    private static Dictionary<ulong, Func<BinaryReaderEx, Symbol, PropertyValue>> Load()
    {
        var assembly = typeof(PropertyValueLayouts).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        var layouts = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        return layouts.ToDictionary(layout => TelltaleHash.ComputeCrc64(layout.Key), layout => Compile(layout.Value));
    }

    private static Func<BinaryReaderEx, Symbol, PropertyValue> Compile(string layout)
    {
        if (layout == SymbolLayout)
        {
            return (reader, _) => new SymbolValue(reader.ReadSymbol());
        }

        var tokens = layout.Split(' ');
        var parts = new List<Action<BinaryReaderEx, MemoryStream>>();
        for (var index = 0; index < tokens.Length; index++)
        {
            parts.Add(tokens[index] switch
            {
                Block => ReadBlock,
                Count => Repeated(int.Parse(tokens[++index])),
                var size => Fixed(int.Parse(size)),
            });
        }

        return (reader, type) =>
        {
            using var buffer = new MemoryStream();
            foreach (var part in parts)
            {
                part(reader, buffer);
            }

            return new RawBytesValue(buffer.ToArray(), type);
        };
    }

    private static void ReadBlock(BinaryReaderEx reader, MemoryStream buffer)
    {
        var size = reader.ReadUInt32();
        if (size < sizeof(uint) || size - sizeof(uint) > reader.Remaining)
        {
            throw new InvalidDataException($"Property value block of {size} bytes at position {reader.Position} is invalid.");
        }

        buffer.Write(BitConverter.GetBytes(size));
        buffer.Write(reader.ReadBytes((int)(size - sizeof(uint))));
    }

    private static Action<BinaryReaderEx, MemoryStream> Repeated(int elementSize) => (reader, buffer) =>
    {
        var count = reader.ReadUInt32();
        if (count * (long)elementSize > reader.Remaining)
        {
            throw new InvalidDataException($"Property value with {count} elements at position {reader.Position} is truncated.");
        }

        buffer.Write(BitConverter.GetBytes(count));
        buffer.Write(reader.ReadBytes((int)(count * elementSize)));
    };

    private static Action<BinaryReaderEx, MemoryStream> Fixed(int size) => (reader, buffer) => buffer.Write(reader.ReadBytes(size));
}
