using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StorySaveFactory : IStorySaveFactory
{
    private const uint LocalKeysFlag = 0x100;

    public SaveSlot Create(string fileName, int episode, StorySeason season)
    {
        var metadata = new PropertySet
        {
            Flags = LocalKeysFlag,
            ParentSymbols = [Symbol.FromString(season.SlotMetadataParent)],
        };
        metadata.SetInt(SlotMetadataKeys.LatestSerial, 0);
        metadata.SetInt(SlotMetadataKeys.EpisodeInProgress, episode);
        metadata.SetString(SlotMetadataKeys.LatestSave, string.Empty);

        return SaveSlotFactory.Create(fileName, (BundleFileNames.SlotMetadata, metadata));
    }
}
