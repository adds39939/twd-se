// Quick script to dump metadata from both bundle types
#r "../src/TwdSaveEditor.Core/bin/Debug/net10.0/TwdSaveEditor.Core.dll"
#r "../src/TwdSaveEditor.Core.Binary/bin/Debug/net10.0/TwdSaveEditor.Core.Binary.dll"

using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;

void DumpMetadata(string path, string label)
{
    var data = File.ReadAllBytes(path);
    var slot = BundleReader.Read(data, Path.GetFileName(path));
    Console.WriteLine($"\n=== {label} ===");
    if (slot.Metadata == null) { Console.WriteLine("  No metadata"); return; }
    foreach (var g in slot.Metadata.TypeGroups)
    {
        Console.WriteLine($"  TypeGroup: {g.TypeSymbol}");
        foreach (var p in g.Properties)
        {
            var val = p.Value switch
            {
                IntValue iv => iv.Value.ToString(),
                StringValue sv => $"\"{sv.Value}\"",
                BoolValue bv => bv.Value.ToString(),
                _ => p.Value?.ToString() ?? "null"
            };
            Console.WriteLine($"    0x{p.KeySymbol.Value:X16} = {val}");
        }
    }
}

DumpMetadata(@"tests\TestData\S1\wd1_saveslot1_live.bundle", "Slot: metadata_slot.p");
DumpMetadata(@"tests\TestData\S1\_wd1_saveslot1_autosave.bundle", "Autosave: metadata_save.p");
