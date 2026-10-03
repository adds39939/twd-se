using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Tools.DumpMetadata.Metadata;

public static class MetadataDumper
{
    public static void Dump(string path, string label, TextWriter output)
    {
        var slot = BundleReader.Read(File.ReadAllBytes(path), Path.GetFileName(path));
        output.WriteLine();
        output.WriteLine($"=== {label} ===");

        if (slot.Metadata == null)
        {
            output.WriteLine("  No metadata");
            return;
        }

        foreach (var group in slot.Metadata.TypeGroups)
        {
            output.WriteLine($"  TypeGroup: {group.TypeSymbol}");
            foreach (var property in group.Properties)
            {
                output.WriteLine($"    0x{property.KeySymbol.Value:X16} = {Describe(property.Value)}");
            }
        }
    }

    private static string Describe(PropertyValue value) => value switch
    {
        IntValue number => number.Value.ToString(),
        StringValue text => $"\"{text.Value}\"",
        BoolValue flag => flag.Value.ToString(),
        _ => value.ToString() ?? "null",
    };
}
