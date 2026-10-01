using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class BundleWriter
{
    public const int EntrySize = 40;

    private const uint BundleVersion = 1;
    private const int DebugBytesPerFile = 8;

    public static byte[] Write(SaveSlot slot)
    {
        var data = slot.Files.ToDictionary(file => file, Encode);

        var offsets = new Dictionary<BundleFileEntry, uint>();
        using var content = new MemoryStream();
        foreach (var file in slot.Files.OrderBy(file => file.OriginalOffset ?? uint.MaxValue))
        {
            offsets[file] = (uint)content.Position;
            content.Write(data[file]);
        }

        using var table = new MemoryStream();
        using (var writer = new BinaryWriterEx(table, leaveOpen: true))
        {
            writer.WriteUInt32(BundleVersion);
            writer.WriteUInt32((uint)slot.Files.Count);
            foreach (var file in slot.Files)
            {
                writer.WriteUInt32(offsets[file]);
                writer.WriteUInt32((uint)data[file].Length);
                writer.WriteBytes(file.NameField);
                writer.WriteUInt64(file.NameSymbol);
                writer.WriteUInt64(file.TypeSymbol);
            }
        }

        var debug = new byte[slot.Files.Count * DebugBytesPerFile];
        return MetaStreamCodec.Write(new MetaStreamContent(slot.OuterHeader, table.ToArray(), debug, content.ToArray()));
    }

    private static byte[] Encode(BundleFileEntry file) =>
        file.Properties != null ? PropertyFileCodec.Write(file.Data, file.Properties) : file.Data;
}
