using System.Globalization;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Meta;

public sealed class MetaDumper(SymbolNames names, MetaReader reader, TextWriter output)
{
    private const int MaxInlineBytes = 64;

    public void DumpStream(ReadOnlySpan<byte> data, string rootType)
    {
        var document = reader.Read(data, rootType);
        if (document == null)
        {
            Line(0, $"not a MetaStream ({data.Length} bytes): {Preview(data)}");
            return;
        }

        WriteDocument(document, 0);
    }

    private void WriteDocument(MetaDocument document, int indent)
    {
        var sections = document.Sections;
        Line(indent, $"{MetaStreamParser.MagicName(sections.Magic)} default={sections.Default.Length} debug={sections.Debug.Length} async={sections.Async.Length}");
        foreach (var entry in sections.VersionEntries)
            Line(indent, $"class {reader.Types.Find(entry.Type) ?? $"#{entry.Type:X16}"} v{entry.Version:X8}");

        if (document.Root != null)
            WriteNode(document.Root, indent);

        if (document.Error != null)
            Line(indent, $"!! {document.Error} at offset {document.ErrorOffset} of {sections.Default.Length}");
        else if (document.TrailingBytes > 0)
            Line(indent, $"trailing {document.TrailingBytes} bytes");
    }

    private void WriteNode(MetaNode node, int indent)
    {
        switch (node)
        {
            case MetaBundle bundle:
                Line(indent, $"bundle version={bundle.Version} files={bundle.Files.Count}");
                foreach (var file in bundle.Files)
                {
                    Line(indent, $"[{file.Name}] {names.Describe(file.NameSymbol)} type={file.Type ?? $"#{file.TypeSymbol:X16}"} offset={file.Offset} size={file.Size}");
                    if (file.Content != null)
                        WriteDocument(file.Content, indent + 1);
                    else
                        Line(indent + 1, "!! not parsed");
                }

                break;
            case MetaPropertySet set:
                Line(indent, $"PropertySet v{set.Version} flags=0x{set.Flags:X} size={set.Size} parents=[{string.Join(", ", set.Parents.Select(names.Describe))}]");
                foreach (var property in set.Properties)
                    WriteNamed($"{names.Describe(property.Key)} <{property.Type}>", property.Value, indent + 1);

                if (set.Error != null)
                    Line(indent + 1, $"!! {set.Error}");

                break;
            case MetaList list:
                Line(indent, $"[{list.Items.Count}]");
                foreach (var item in list.Items)
                    WriteNode(item, indent + 1);

                break;
            case MetaMap map:
                Line(indent, $"{{{map.Entries.Count}}}");
                foreach (var (key, value) in map.Entries)
                    WriteNamed(FormatScalar(key) ?? "<key>", value, indent + 1);

                break;
            case MetaObject value:
                Line(indent, value.Type);
                foreach (var (name, member) in value.Members)
                    WriteNamed(name, member, indent + 1);

                break;
            default:
                Line(indent, FormatScalar(node) ?? node.ToString()!);
                break;
        }
    }

    private void WriteNamed(string name, MetaNode value, int indent)
    {
        if (FormatScalar(value) is { } scalar)
        {
            Line(indent, $"{name} = {scalar}");
            return;
        }

        Line(indent, $"{name}:");
        WriteNode(value, indent + 1);
    }

    private string? FormatScalar(MetaNode node) => node switch
    {
        MetaSymbol { IsHandle: true } symbol => $"handle {names.Describe(symbol.Hash)}",
        MetaSymbol symbol => names.Describe(symbol.Hash),
        MetaScalar { Value: string text } => TextFormat.QuoteString(text),
        MetaScalar { Value: bool flag } => flag ? "true" : "false",
        MetaScalar { Value: float number } => number.ToString("R", CultureInfo.InvariantCulture),
        MetaScalar { Value: double number } => number.ToString("R", CultureInfo.InvariantCulture),
        MetaScalar scalar => Convert.ToString(scalar.Value, CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string Preview(ReadOnlySpan<byte> data) =>
        Bytes.Hex(data[..Math.Min(data.Length, MaxInlineBytes)]) + (data.Length > MaxInlineBytes ? "..." : string.Empty);

    private void Line(int indent, string text) => output.WriteLine(new string(' ', indent * 2) + text);
}
