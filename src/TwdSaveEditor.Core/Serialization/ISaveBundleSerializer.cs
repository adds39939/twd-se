using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Serialization;

public interface ISaveBundleSerializer
{
    SaveSlot Read(byte[] data, string fileName);

    byte[] Write(SaveSlot slot);

    bool CanPatchMetadata(SaveSlot slot);

    byte[] PatchMetadata(SaveSlot slot);
}
