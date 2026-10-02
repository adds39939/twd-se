using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.S3.Saves;

public static class S3SaveFactory
{
    private const string SlotMetadataParent = "metadata_slot_s3.prop";
    private const uint LocalKeysFlag = 0x100;

    public static SaveSlot Create(string fileName, int episode)
    {
        var metadata = new PropertySet
        {
            Flags = LocalKeysFlag,
            ParentSymbols = [Symbol.FromString(SlotMetadataParent)],
        };
        metadata.SetInt(SlotMetadataKeys.LatestSerial, 0);
        metadata.SetInt(SlotMetadataKeys.EpisodeInProgress, episode);
        metadata.SetString(SlotMetadataKeys.LatestSave, string.Empty);

        return SaveSlotFactory.Create(fileName, (BundleFileNames.SlotMetadata, metadata));
    }
}
