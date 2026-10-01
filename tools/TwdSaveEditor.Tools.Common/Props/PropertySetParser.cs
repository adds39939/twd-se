using System.Text;

namespace TwdSaveEditor.Tools.Common.Props;

public static class PropertySetParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static ParsedPropertySet? Parse(PropReader reader)
    {
        if (reader.Remaining < 16)
            return null;

        var version = reader.U32();
        var flags = reader.U32();
        var dataSize = reader.U32();
        var dataEnd = reader.Position + dataSize;

        var parentCount = reader.U32();
        for (uint i = 0; i < parentCount; i++)
        {
            if (reader.Remaining < 8)
            {
                reader.Position = dataEnd;
                return null;
            }

            reader.U64();
        }

        if (reader.Remaining < 4)
        {
            reader.Position = dataEnd;
            return null;
        }

        var groupCount = reader.U32();
        var groups = new List<ParsedTypeGroup>();

        for (uint g = 0; g < groupCount; g++)
        {
            if (reader.Remaining < 12)
                break;

            var type = reader.U64();
            var propertyCount = reader.U32();
            var properties = new List<ParsedProperty>();

            for (uint p = 0; p < propertyCount; p++)
            {
                if (reader.Remaining < 8)
                    break;

                var key = reader.U64();
                if (!PropTypes.IsKnown(type))
                {
                    reader.Position = dataEnd;
                    return new ParsedPropertySet(version, flags, groups);
                }

                properties.Add(new ParsedProperty(key, ParseValue(reader, type)));
            }

            groups.Add(new ParsedTypeGroup(type, properties));
        }

        return new ParsedPropertySet(version, flags, groups);
    }

    private static object? ParseValue(PropReader reader, ulong type) => type switch
    {
        PropTypes.String => ParseString(reader),
        PropTypes.Bool => reader.U8() == 0x31,
        PropTypes.Int32 => reader.I32(),
        _ => Parse(reader),
    };

    private static string ParseString(PropReader reader)
    {
        var raw = reader.Read(reader.U32());
        try
        {
            return StrictUtf8.GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(raw);
        }
    }
}
