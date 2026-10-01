using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Props;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.ValidateEditedSaves.Bundles;

public static class MetadataProperties
{
    private static readonly Dictionary<ulong, string> Names = new()
    {
        [0xB218E7C003A67CE9] = "Episode in Progress",
        [0x7E7BE4FD8F464350] = "Saved Game Episode",
        [0x6047826CD4EDC6B4] = "Checkpoint Dialog",
        [0x8B8C42FEDDCE350C] = "Checkpoint Dialog Node",
        [0x98A7E965982E6E98] = "Saved Game Chapter ID",
        [0xF235E9FCE9562E01] = "Latest Save",
        [0x7C725227A47FD1BA] = "Latest Serial",
        [0x94C245DACB1ADDC3] = "progress",
        [0x5C36A605E4E7890F] = "Saved Game Date",
        [0x037B536D294D9D9A] = "Saved Game Serial",
    };

    public static OrderedDictionary<string, object>? TryParse(ReadOnlySpan<byte> inner, long position)
    {
        try
        {
            return Parse(inner, position);
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    private static OrderedDictionary<string, object> Parse(ReadOnlySpan<byte> inner, long position)
    {
        var properties = new OrderedDictionary<string, object>();
        position += 4 + Bytes.U32(inner, position) * 8L;
        var groupCount = Bytes.U32(inner, position);
        position += 4;

        for (uint group = 0; group < groupCount; group++)
        {
            var type = Bytes.U64(inner, position);
            var propertyCount = Bytes.U32(inner, position + 8);
            position += 12;

            for (uint property = 0; property < propertyCount; property++)
            {
                var key = Bytes.U64(inner, position);
                position += 8;
                var name = Names.GetValueOrDefault(key, $"0x{key:X16}");

                switch (type)
                {
                    case PropTypes.Int32:
                        properties[name] = Bytes.I32(inner, position);
                        position += 4;
                        break;
                    case PropTypes.String:
                        var length = Bytes.U32(inner, position);
                        position += 4;
                        properties[name] = TextFormat.DecodeUtf8(Bytes.Slice(inner, position, position + length));
                        position += length;
                        break;
                    case PropTypes.Bool:
                        properties[name] = inner[checked((int)position)] == 0x31;
                        position += 1;
                        break;
                }
            }
        }

        return properties;
    }
}
