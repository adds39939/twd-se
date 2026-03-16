using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Deserializes a Telltale PropertySet from binary data.
/// Supports Definitive Series format: version(u32) + flags(u32) + data_size(u32) prefix.
/// </summary>
public sealed class PropertySetReader
{
    public PropertySet Read(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReaderEx(ms);
        return ReadPropertySet(reader);
    }

    private PropertySet ReadPropertySet(BinaryReaderEx reader)
    {
        var propSet = new PropertySet();

        // Version/flags/data_size prefix (Definitive Series v2 format)
        propSet.Version = reader.ReadUInt32();
        propSet.Flags = reader.ReadUInt32();
        var dataSize = reader.ReadUInt32(); // remaining bytes from this point

        // Parent symbols
        var parentCount = reader.ReadUInt32();
        for (uint i = 0; i < parentCount; i++)
            propSet.ParentSymbols.Add(reader.ReadSymbol());

        // Type groups
        var typeGroupCount = reader.ReadUInt32();
        for (uint g = 0; g < typeGroupCount; g++)
        {
            var typeSymbol = reader.ReadSymbol();
            var propCount = reader.ReadUInt32();
            var group = new TypeGroup(typeSymbol);

            for (uint p = 0; p < propCount; p++)
            {
                var keySymbol = reader.ReadSymbol();
                var value = ReadValue(reader, typeSymbol);
                group.Properties.Add(new Property(keySymbol, value));
            }

            propSet.TypeGroups.Add(group);
        }

        return propSet;
    }

    private PropertyValue ReadValue(BinaryReaderEx reader, Symbol typeSymbol)
    {
        var hash = typeSymbol.Value;

        if (hash == TelltaleTypes.Bool)
            return new BoolValue(reader.ReadTelltaleBool());

        if (hash == TelltaleTypes.Int32)
            return new IntValue(reader.ReadInt32());

        if (hash == TelltaleTypes.Float)
            return new FloatValue(reader.ReadFloat());

        if (hash == TelltaleTypes.String)
            return new StringValue(reader.ReadLengthPrefixedString());

        if (hash == TelltaleTypes.Symbol)
            return new SymbolValue(reader.ReadSymbol());

        if (hash == TelltaleTypes.Flags)
            return new IntValue(reader.ReadInt32()); // Flags serialize as u32

        if (hash == TelltaleTypes.PropertySet)
            return new PropertySetValue(ReadPropertySet(reader));

        if (hash == TelltaleTypes.ChoicesContainer)
            return ReadChoicesContainer(reader, typeSymbol);

        // Unknown type — capture raw bytes for round-tripping
        return ReadRawValue(reader, typeSymbol);
    }

    /// <summary>
    /// Read a ChoicesContainer (DCArray of String+Bool pairs).
    /// Format: u32(count) + count × (u32(strlen) + chars + u8(bool))
    /// The first u32 is element count, NOT byte length.
    /// </summary>
    private static RawBytesValue ReadChoicesContainer(BinaryReaderEx reader, Symbol typeSymbol)
    {
        var startPos = reader.Position;
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

    private static RawBytesValue ReadRawValue(BinaryReaderEx reader, Symbol typeSymbol)
    {
        // For unknown types, try u32 length prefix + data
        var startPos = reader.Position;
        var length = reader.ReadInt32();

        if (length >= 0 && length <= reader.Remaining)
        {
            var data = reader.ReadBytes(length);
            var fullData = new byte[4 + data.Length];
            BitConverter.GetBytes(length).CopyTo(fullData, 0);
            data.CopyTo(fullData, 4);
            return new RawBytesValue(fullData, typeSymbol);
        }

        throw new InvalidDataException(
            $"Unknown type 0x{typeSymbol.Value:X16} at position {startPos} with invalid length {length}");
    }
}
