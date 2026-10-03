using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.PropertySets;

public sealed class PropertySetReader
{
    public const int MaxDepth = 16;

    public PropertySet Read(byte[] data) => MalformedData.Guard(() =>
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReaderEx(ms);
        var propSet = ReadPropertySet(reader, 1);
        if (reader.Remaining != 0)
        {
            throw new InvalidDataException($"PropertySet has {reader.Remaining} unread bytes.");
        }

        return propSet;
    });

    private PropertySet ReadPropertySet(BinaryReaderEx reader, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidDataException($"PropertySet is nested more than {MaxDepth} levels deep.");
        }

        var propSet = new PropertySet();

        propSet.Version = reader.ReadUInt32();
        propSet.Flags = reader.ReadUInt32();
        var blockEnd = reader.Position + reader.ReadUInt32();

        var parentCount = reader.ReadUInt32();
        for (uint i = 0; i < parentCount; i++)
        {
            propSet.ParentSymbols.Add(reader.ReadSymbol());
        }

        var typeGroupCount = reader.ReadUInt32();
        for (uint g = 0; g < typeGroupCount; g++)
        {
            var typeSymbol = reader.ReadSymbol();
            var propCount = reader.ReadUInt32();
            var group = new TypeGroup(typeSymbol);

            for (uint p = 0; p < propCount; p++)
            {
                var keySymbol = reader.ReadSymbol();
                var value = ReadValue(reader, typeSymbol, depth);
                group.Properties.Add(new Property(keySymbol, value));
            }

            propSet.TypeGroups.Add(group);
        }

        if (reader.Position != blockEnd)
        {
            throw new InvalidDataException($"PropertySet block ends at {blockEnd}, read to {reader.Position}.");
        }

        return propSet;
    }

    private PropertyValue ReadValue(BinaryReaderEx reader, Symbol typeSymbol, int depth)
    {
        var hash = typeSymbol.Value;

        if (hash == TelltaleTypes.Bool)
        {
            return new BoolValue(reader.ReadTelltaleBool());
        }

        if (hash == TelltaleTypes.Int32)
        {
            return new IntValue(reader.ReadInt32());
        }

        if (hash == TelltaleTypes.Float)
        {
            return new FloatValue(reader.ReadFloat());
        }

        if (hash == TelltaleTypes.String)
        {
            return new StringValue(reader.ReadLengthPrefixedString());
        }

        if (hash == TelltaleTypes.Symbol)
        {
            return new SymbolValue(reader.ReadSymbol());
        }

        if (hash == TelltaleTypes.Flags)
        {
            return new IntValue(reader.ReadInt32());
        }

        if (hash == TelltaleTypes.PropertySet)
        {
            return new PropertySetValue(ReadPropertySet(reader, depth + 1));
        }

        if (hash == TelltaleTypes.StringArray)
        {
            return ReadStringArray(reader);
        }

        if (hash == TelltaleTypes.ChoicesContainer)
        {
            return ReadChoicesContainer(reader, typeSymbol);
        }

        return PropertyValueLayouts.Read(reader, typeSymbol);
    }

    private static StringArrayValue ReadStringArray(BinaryReaderEx reader)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > reader.Remaining / sizeof(int))
        {
            throw new InvalidDataException($"String array with invalid count {count} at position {reader.Position}.");
        }

        var values = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(reader.ReadLengthPrefixedString());
        }

        return new StringArrayValue(values);
    }

    private static RawBytesValue ReadChoicesContainer(BinaryReaderEx reader, Symbol typeSymbol)
    {
        var count = reader.ReadUInt32();

        using var buffer = new MemoryStream();
        using var bw = new BinaryWriter(buffer);
        bw.Write(count);

        for (uint i = 0; i < count; i++)
        {
            var strLen = reader.ReadInt32();
            var strBytes = reader.ReadBytes(strLen);
            var boolByte = reader.ReadByte();
            bw.Write(strLen);
            bw.Write(strBytes);
            bw.Write(boolByte);
        }

        bw.Flush();
        return new RawBytesValue(buffer.ToArray(), typeSymbol);
    }
}
