using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;

namespace TwdSaveEditor.Core.Binary.Bundles;

public sealed class SaveBundleSerializer : ISaveBundleSerializer
{
    public SaveSlot Read(byte[] data, string fileName) => BundleReader.Read(data, fileName);

    public byte[] Write(SaveSlot slot) => BundleWriter.Write(slot);
}
