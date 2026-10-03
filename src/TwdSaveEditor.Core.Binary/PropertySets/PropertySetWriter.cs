using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.PropertySets;

public sealed class PropertySetWriter
{
    public byte[] Write(PropertySet propSet)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(propSet.Version);
        writer.WriteUInt32(propSet.Flags);

        byte[] bodyBytes;
        using (var bodyMs = new MemoryStream())
        using (var bodyWriter = new BinaryWriterEx(bodyMs, leaveOpen: true))
        {
            WritePropertySetBody(bodyWriter, propSet);
            bodyWriter.Flush();
            bodyBytes = bodyMs.ToArray();
        }

        writer.WriteUInt32((uint)(bodyBytes.Length + 4));
        writer.WriteBytes(bodyBytes);

        writer.Flush();
        return ms.ToArray();
    }

    private void WritePropertySetBody(BinaryWriterEx writer, PropertySet propSet)
    {
        writer.WriteUInt32((uint)propSet.ParentSymbols.Count);
        foreach (var parent in propSet.ParentSymbols)
        {
            writer.WriteSymbol(parent);
        }

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
                var nested = Write(ps.Value);
                writer.WriteBytes(nested);
                break;
            case StringArrayValue array:
                writer.WriteInt32(array.Values.Count);
                foreach (var item in array.Values)
                {
                    writer.WriteLengthPrefixedString(item);
                }

                break;
            case RawBytesValue raw:
                writer.WriteBytes(raw.Data);
                break;
        }
    }
}
