using System.Text;

namespace TwdSaveEditor.Tools.Common.Text;

public static class TextFormat
{
    private const char Replacement = '�';

    public static string DecodeAscii(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder(data.Length);
        foreach (var b in data)
            builder.Append(b < 0x80 ? (char)b : Replacement);

        return builder.ToString();
    }

    public static string DecodeUtf8(ReadOnlySpan<byte> data) => Encoding.UTF8.GetString(data);

    public static string DecodeLatin1(ReadOnlySpan<byte> data) => Encoding.Latin1.GetString(data);

    public static bool IsWhitespace(char c) => char.IsWhiteSpace(c) || c is >= '\x1c' and <= '\x1f';

    public static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsWhitespace(text[start]))
            start++;
        while (end > start && IsWhitespace(text[end - 1]))
            end--;

        return text[start..end];
    }

    public static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    public static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var position = text.IndexOf(value, StringComparison.Ordinal);
        while (position >= 0)
        {
            count++;
            position = text.IndexOf(value, position + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    public static string QuoteBytes(ReadOnlySpan<byte> data)
    {
        var quote = data.Contains((byte)'\'') && !data.Contains((byte)'"') ? '"' : '\'';
        var builder = new StringBuilder("b").Append(quote);
        foreach (var b in data)
        {
            var c = (char)b;
            if (c == quote || c == '\\')
                builder.Append('\\').Append(c);
            else if (c == '\t')
                builder.Append("\\t");
            else if (c == '\n')
                builder.Append("\\n");
            else if (c == '\r')
                builder.Append("\\r");
            else if (b < 0x20 || b >= 0x7F)
                builder.Append("\\x").Append(b.ToString("x2"));
            else
                builder.Append(c);
        }

        return builder.Append(quote).ToString();
    }

    public static string QuoteString(string text)
    {
        var quote = text.Contains('\'') && !text.Contains('"') ? '"' : '\'';
        var builder = new StringBuilder().Append(quote);
        foreach (var c in text)
        {
            if (c == quote || c == '\\')
                builder.Append('\\').Append(c);
            else if (c == '\t')
                builder.Append("\\t");
            else if (c == '\n')
                builder.Append("\\n");
            else if (c == '\r')
                builder.Append("\\r");
            else if (c < 0x20 || c is >= '\x7f' and <= '\xa0')
                builder.Append("\\x").Append(((int)c).ToString("x2"));
            else
                builder.Append(c);
        }

        return builder.Append(quote).ToString();
    }

    public static string QuoteList(IEnumerable<string> items) =>
        "[" + string.Join(", ", items.Select(QuoteString)) + "]";
}
