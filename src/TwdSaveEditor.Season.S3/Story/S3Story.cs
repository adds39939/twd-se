using TwdSaveEditor.Season.Base.Story;

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
    };

    public static readonly StoryResumePoint Resume = new(Season);
}
