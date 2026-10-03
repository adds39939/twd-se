using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class SaveSlotFactory
{
    private const uint ResourceBundleVersion = 0x5A585C97;
    private const uint SymbolVersion = 0xB539B0FF;

    public static SaveSlot Create(string fileName, params (string Name, PropertySet Properties)[] files) => new()
    {
        FilePath = fileName,
        FileName = fileName,
        OuterHeader = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries =
            [
                new VersionEntry(TelltaleTypes.ResourceBundle, ResourceBundleVersion),
                new VersionEntry(TelltaleTypes.Symbol, SymbolVersion),
            ],
        },
        Files = files.Select(file => CreateFile(file.Name, file.Properties)).ToList(),
    };

    public static BundleFileEntry CreateFile(string name, PropertySet properties)
    {
        var file = BundleFileEntry.Create(name, TelltaleTypes.PropertySet, []);
        file.Properties = properties;
        return file;
    }
}
