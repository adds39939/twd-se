using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.S4.Story;

public static class S4Story
{
    public static readonly StorySeason Season = new(typeof(S4Story).Assembly)
    {
        LastEpisode = 4,
        ProjectPrefix = "WalkingDead40",
        SlotMetadataParent = "metadata_slot_s4.prop",
        SaveMetadataParent = "metadata_save_s4.prop",
        SharedResourceSets = ["Menu", "UISeason4", "MenuSeason4", "ProjectSeason4"],
        DateFormat = "yyyy-MM-dd HH:mm",
        PreviousGameData = true,
        PreviousSeasonKey = "s3",
        FinishedEpisodeKind = StoryNumberKind.Integer,
        GameLogicVisible = true,
        ScriptProperties = StoryFiles.SystemsProperties,
        SaveLoadProperties = StoryFiles.SystemsProperties,
    };

    public static readonly StoryResumePoint Resume = new(Season);
}
