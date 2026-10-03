using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.S3.Saves;

namespace TwdSaveEditor.Season.S3.Story;

public static class S3Story
{
    public static readonly StorySeason Season = new(typeof(S3Story).Assembly)
    {
        LastEpisode = 5,
        ProjectPrefix = "WalkingDead30",
        SlotMetadataParent = "metadata_slot_s3.prop",
        SaveMetadataParent = "metadata_save_s3.prop",
        SharedResourceSets = ["UISeason3", "MenuSeason3", "ProjectSeason3"],
        DateFormat = "yyyy-MM-dd HH:mm",
        PreviousGameData = true,
        PreviousSeasonKey = "s2",
    };

    public static readonly StoryResumePoint Resume = new(Season);

    public static readonly StoryInventory Inventory = new(
        Season,
        Resume,
        "Javier",
        [S3SlotFiles.OwnerInventoryProperties, S3SlotFiles.InventoryProperties],
        "Javier has no items in this episode; only Episodes 1 and 2 have items.");
}
