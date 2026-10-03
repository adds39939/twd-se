using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S1.Persistence;

namespace TwdSaveEditor.Season.S2.Saves;

public sealed class S2SaveFactory : IS2SaveFactory
{
    private const string SlotMetadataParent = "metadata_slot_s2.prop";
    private const uint LocalKeysFlag = 0x100;

    public SaveSlot Create(string fileName, string episodeId)
    {
        var metadata = new PropertySet
        {
            Flags = LocalKeysFlag,
            ParentSymbols = [Symbol.FromString(SlotMetadataParent)],
        };
        metadata.SetInt(SlotMetadataKeys.LatestSerial, 0);
        metadata.SetString(SlotMetadataKeys.EpisodeInProgress, episodeId);
        metadata.SetString(SlotMetadataKeys.LatestSave, string.Empty);

        var imported = new PropertySet { Flags = LocalKeysFlag };
        var container = new Symbol(TelltaleTypes.ChoicesContainer);
        for (var episode = PersistentKeys.FirstEpisode; episode <= PersistentKeys.LastEpisode; episode++)
            imported.Set(Symbol.FromString(S2SlotFiles.TrackerContainer(episode)), container, new RawBytesValue(ChoicesContainer.Serialize([]), container));

        return SaveSlotFactory.Create(fileName,
            (BundleFileNames.SlotMetadata, metadata),
            (BundleFileNames.Season1Choices, imported));
    }
}
