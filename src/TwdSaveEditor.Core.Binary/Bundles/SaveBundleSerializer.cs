using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;

namespace TwdSaveEditor.Core.Binary.Bundles;

public sealed class SaveBundleSerializer : ISaveBundleSerializer
{
    public SaveSlot Read(byte[] data, string fileName) => BundleReader.Read(data, fileName);

    public byte[] Write(SaveSlot slot) => BundleWriter.Write(slot);

    public bool CanPatchMetadata(SaveSlot slot)
        => slot.RawBundleData != null && slot.Metadata != null && slot.RawMetadataFile != null;

    public byte[] PatchMetadata(SaveSlot slot)
    {
        if (!CanPatchMetadata(slot))
            throw new InvalidOperationException("Cannot patch metadata: missing raw bundle data.");

        return MetadataPatcher.PatchMetadata(slot.RawBundleData!, slot.Metadata!, slot.RawMetadataFile!);
    }
}
