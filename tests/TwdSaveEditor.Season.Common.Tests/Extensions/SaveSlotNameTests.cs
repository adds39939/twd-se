using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Common.Tests.Extensions;

public class SaveSlotNameTests
{
    [Theory]
    [InlineData("s1", 3)]
    [InlineData("s1_400days", 3)]
    [InlineData("s2", 3)]
    [InlineData("s3", 4)]
    [InlineData("s4", 4)]
    [InlineData("michonne", 4)]
    public void SaveSlotCount_MatchesTheGameMenu(string seasonKey, int slots)
    {
        Assert.Equal(slots, TestSeasons.Registry.Get(seasonKey)!.SaveSlotCount);
    }

    [Theory]
    [InlineData("s1", 2, "wd1_saveslot2.bundle")]
    [InlineData("s1_400days", 1, "wd1_saveslot1.bundle")]
    [InlineData("s2", 3, "wd2_saveslot3.bundle")]
    [InlineData("s3", 4, "wd3_saveslot4.bundle")]
    [InlineData("s4", 5, "wd4_saveslot5.bundle")]
    [InlineData("michonne", 1, "wdm_saveslot1.bundle")]
    public void SaveSlotFileName_UsesTheSeasonPrefix(string seasonKey, int slot, string expected)
    {
        Assert.Equal(expected, TestSeasons.Registry.Get(seasonKey)!.SaveSlotFileName(slot));
    }

    [Fact]
    public void NextFreeSlot_IsOneInAnEmptyFolder()
    {
        Assert.Equal(1, TestSeasons.Registry.Get("s3")!.NextFreeSlot([]));
    }

    [Fact]
    public void NextFreeSlot_SkipsSlotsThatHaveAnyFile()
    {
        string[] files =
        [
            "wd1_saveslot1.bundle",
            "_wd1_saveslot1_autosave.bundle",
            "_wd1_saveslot2_autosave.bundle",
            "wd2_saveslot3.bundle",
        ];

        Assert.Equal(3, TestSeasons.Registry.Get("s1")!.NextFreeSlot(files));
    }

    [Fact]
    public void NextFreeSlot_MatchesFileNamesInAnyCase()
    {
        string[] files = ["wd2_saveSlot1.bundle", "_WD2_SAVESLOT2_ID.estore", "_wd2_saveslot3_id_Page913.epage"];

        Assert.Equal(4, TestSeasons.Registry.Get("s2")!.NextFreeSlot(files));
    }

    [Fact]
    public void NextFreeSlot_OnlyCountsWholeSlotNumbers()
    {
        string[] files = ["wd4_saveslot10.bundle", "wd4_saveslot1x.bundle", "wd4_saveslot.bundle"];

        Assert.Equal(1, TestSeasons.Registry.Get("s4")!.NextFreeSlot(files));
    }

    [Fact]
    public void NextFreeSlot_GoesPastTheSlotsTheGameShows()
    {
        var season = TestSeasons.Registry.Get("michonne")!;
        var files = Enumerable.Range(1, season.SaveSlotCount).Select(season.SaveSlotFileName);

        Assert.Equal(season.SaveSlotCount + 1, season.NextFreeSlot(files));
    }

    [Fact]
    public void NextFreeSlot_400DaysSharesTheSeason1Slots()
    {
        Assert.Equal(2, TestSeasons.Registry.Get("s1_400days")!.NextFreeSlot(["wd1_saveslot1.bundle"]));
    }
}
