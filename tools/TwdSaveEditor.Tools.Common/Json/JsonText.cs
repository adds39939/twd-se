using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TwdSaveEditor.Tools.Common.Json;

public static class JsonText
{
    private const int IndentSize = 2;

    public static string Serialize(JsonNode? node, bool escapeNonAscii)
    {
        var builder = new StringBuilder();
        Write(builder, node, 0, escapeNonAscii);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonNode? node, int depth, bool escapeNonAscii)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject { Count: 0 }:
                builder.Append("{}");
                break;
            case JsonArray { Count: 0 }:
                builder.Append("[]");
                break;
            case JsonObject obj:
                builder.Append('{');
                var firstProperty = true;
                foreach (var (key, value) in obj)
                {
                    builder.Append(firstProperty ? "\n" : ",\n").Append(' ', (depth + 1) * IndentSize);
                    WriteString(builder, key, escapeNonAscii);
                    builder.Append(": ");
                    Write(builder, value, depth + 1, escapeNonAscii);
                    firstProperty = false;
                }

                builder.Append('\n').Append(' ', depth * IndentSize).Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in array)
                {
                    builder.Append(firstItem ? "\n" : ",\n").Append(' ', (depth + 1) * IndentSize);
                    Write(builder, item, depth + 1, escapeNonAscii);
                    firstItem = false;
                }

                builder.Append('\n').Append(' ', depth * IndentSize).Append(']');
                break;
            default:
                if (node.GetValueKind() == JsonValueKind.String)
                {
                    WriteString(builder, node.GetValue<string>(), escapeNonAscii);
                }
                else
                {
                    builder.Append(node.ToJsonString());
                }

                break;
        }
    }

    private static void WriteString(StringBuilder builder, string text, bool escapeNonAscii)
    {
        builder.Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (c < 0x20 || (escapeNonAscii && c > 0x7F))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
