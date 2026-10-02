using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.Michonne.Story;

public static class MichonneStory
{
    public static readonly StorySeason Season = new(typeof(MichonneStory).Assembly)
    {
        LastEpisode = 3,
        ProjectPrefix = "WalkingDeadM10",
        SlotMetadataParent = "metadata_slot_sm.prop",
        SaveMetadataParent = "metadata_save_sm.prop",
        SharedResourceSets = ["UISeasonM", "MenuSeasonM", "ProjectSeasonM"],
        DateFormat = "yyyy-MM-dd HH:mm:ss",
        ChapterSaves = true,
        FinishedEpisodeAsText = true,
    };

    public static readonly StoryResumePoint Resume = new(Season);

    public static readonly StoryInventory Inventory = new(
        Season,
        Resume,
        "Michonne",
        [StoryFiles.InventoryProperties],
        "Michonne carries no items in this episode; the game only hands out items in Episode 1.");
}
