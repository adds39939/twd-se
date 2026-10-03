using System.Text;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.TtarchDecrypt.Search;

public sealed class ArchiveDataSearch(byte[] data, TextWriter output)
{
    private static readonly (string Signature, string Name)[] Headers =
    [
        ("NCTT", "NCTT"), ("3ATT", "3ATT"), ("4ATT", "4ATT"), ("NCTT", "NCTT-be"), ("3ATT", "3ATT-be"),
    ];

    private static readonly Dictionary<uint, string> Magics = new()
    {
        [0x5454434E] = "NCTT",
        [0x54544133] = "3ATT",
        [0x54544134] = "4ATT",
        [0x5454435A] = "ZCTT",
    };

    private static readonly string[] Extensions = [".d3dtx", ".wav", ".ogg", ".mp3", ".bank", ".scene", ".chore", ".anm"];

    private readonly string _text = TextFormat.DecodeLatin1(data);

    public void Report()
    {
        output.WriteLine();
        output.WriteLine(new string('=', 60));
        output.WriteLine("SEARCHING FOR GAME DATA PATTERNS");
        output.WriteLine(new string('=', 60));

        ReportHeaders();

        ReportMatches(@"\.lua", "'.lua'", RegexOptions.IgnoreCase, limit: 30, before: 60, after: 20, unique: true);
        ReportMatches(@"\.prop", "'.prop'", RegexOptions.IgnoreCase, limit: 20, before: 60, after: 20, unique: true);
        ReportMatches("[Cc]hoice", "'choice'", RegexOptions.None, limit: 20, before: 30, after: 40, unique: true);

        foreach (var extension in Extensions.Where(extension => _text.Contains(extension, StringComparison.OrdinalIgnoreCase)))
        {
            ReportMatches(Regex.Escape(extension), $"'{extension}'", RegexOptions.IgnoreCase, limit: 5, before: 50, after: 10, unique: false);
        }

        ReportStrings();
    }

    private void ReportHeaders()
    {
        var head = Bytes.Slice(data, 0, 32);
        output.WriteLine();
        output.WriteLine($"First 32 bytes (hex): {Bytes.Hex(head)}");
        output.WriteLine($"First 32 bytes (raw): {TextFormat.QuoteBytes(head)}");

        foreach (var (signature, name) in Headers)
        {
            var position = Bytes.IndexOf(data, Encoding.ASCII.GetBytes(signature));
            if (position < 0)
            {
                continue;
            }

            output.WriteLine();
            output.WriteLine($"Found {name} header at offset 0x{position:X}");
            output.WriteLine($"  Context: {Bytes.Hex(Bytes.Slice(data, position, position + 32))}");
        }

        for (var offset = 0; offset < Math.Min(64, data.Length); offset += 4)
        {
            if (!Magics.TryGetValue(Bytes.U32(data, offset), out var name))
            {
                continue;
            }

            output.WriteLine();
            output.WriteLine($"Found {name} magic (LE) at offset 0x{offset:X}");
        }
    }

    private void ReportMatches(string pattern, string label, RegexOptions options, int limit, int before, int after, bool unique)
    {
        var matches = Regex.Matches(_text, pattern, options | RegexOptions.CultureInvariant);
        output.WriteLine();
        output.WriteLine($"{label} occurrences: {matches.Count}");

        var seen = new HashSet<string>();
        foreach (var match in matches.Take(limit))
        {
            var context = Printable(Bytes.Slice(data, match.Index - before, match.Index + match.Length + after));
            if (unique && !seen.Add(context))
            {
                continue;
            }

            output.WriteLine($"  @0x{match.Index:X}: ...{context}...");
        }
    }

    private void ReportStrings()
    {
        output.WriteLine();
        output.WriteLine();
        output.WriteLine("Sample readable strings (>= 8 chars):");
        var strings = Regex.Matches(_text, @"[\x20-\x7e]{8,200}");
        output.WriteLine($"Total readable strings found: {strings.Count}");

        var printed = 0;
        foreach (var match in strings.Take(100))
        {
            if (!match.Value.Any(char.IsAsciiLetter))
            {
                continue;
            }

            output.WriteLine($"  @0x{match.Index:X}: {match.Value}");
            if (++printed >= 50)
            {
                break;
            }
        }
    }

    private static string Printable(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        foreach (var c in TextFormat.DecodeAscii(bytes))
        {
            builder.Append(c is < ' ' or '\x7f' ? '.' : c);
        }

        return builder.ToString();
    }
}
