using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Serializes a PropertySet back to Telltale's binary format.
/// Includes version/flags/data_size prefix for Definitive Series.
/// </summary>
public sealed class PropertySetWriter
{
    public byte[] Write(PropertySet propSet)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        // Write version and flags
        writer.WriteUInt32(propSet.Version);
        writer.WriteUInt32(propSet.Flags);

        // Write body to temp buffer to calculate data_size
        byte[] bodyBytes;
        using (var bodyMs = new MemoryStream())
        using (var bodyWriter = new BinaryWriterEx(bodyMs, leaveOpen: true))
        {
            WritePropertySetBody(bodyWriter, propSet);
            bodyWriter.Flush();
            bodyBytes = bodyMs.ToArray();
        }

        // data_size = size of body + 4 (for the data_size field itself)
        writer.WriteUInt32((uint)(bodyBytes.Length + 4));
        writer.WriteBytes(bodyBytes);

        writer.Flush();
        return ms.ToArray();
    }

    private void WritePropertySetBody(BinaryWriterEx writer, PropertySet propSet)
    {
        // Parent symbols
        writer.WriteUInt32((uint)propSet.ParentSymbols.Count);
        foreach (var parent in propSet.ParentSymbols)
            writer.WriteSymbol(parent);

        // Type groups
        writer.WriteUInt32((uint)propSet.TypeGroups.Count);
        foreach (var group in propSet.TypeGroups)
        {
            writer.WriteSymbol(group.TypeSymbol);
            writer.WriteUInt32((uint)group.Properties.Count);

            foreach (var prop in group.Properties)
            {
                writer.WriteSymbol(prop.KeySymbol);
                WriteValue(writer, prop.Value);
            }
        }
    }

    private void WriteValue(BinaryWriterEx writer, PropertyValue value)
    {
        switch (value)
        {
            case BoolValue b:
                writer.WriteTelltaleBool(b.Value);
                break;
            case IntValue i:
                writer.WriteInt32(i.Value);
                break;
            case FloatValue f:
                writer.WriteFloat(f.Value);
                break;
            case StringValue s:
                writer.WriteLengthPrefixedString(s.Value);
                break;
            case SymbolValue sym:
                writer.WriteSymbol(sym.Value);
                break;
            case UInt64Value u:
                writer.WriteUInt64(u.Value);
                break;
            case PropertySetValue ps:
                // Nested PropertySet: write version+flags+data_size+body
                var nested = Write(ps.Value);
                writer.WriteBytes(nested);
                break;
            case RawBytesValue raw:
                writer.WriteBytes(raw.Data);
                break;
        }
    }
}
