using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S1.Saves;

public sealed class S1SaveFactory(IS1ResumePoint resume) : IS1SaveFactory
{
    private const string SlotMetadataParent = "metadata_slot_s1.prop";
    private const uint LocalKeysFlag = 0x100;

    public SaveSlot Create(string fileName, int episode)
    {
        var metadata = new PropertySet
        {
            Flags = LocalKeysFlag,
            ParentSymbols = [Symbol.FromString(SlotMetadataParent)],
        };

        var slot = SaveSlotFactory.Create(fileName,
            (BundleFileNames.SlotMetadata, metadata),
            (BundleFileNames.Choices, new PropertySet()));

        resume.RestartFromEpisode(slot, episode);
        return slot;
    }
}
