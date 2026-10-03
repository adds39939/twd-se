using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Base.Tests.Resources;

public class ChoiceDatabaseTests
{
    [Fact]
    public void AllChoices_HasEntries()
    {
        Assert.NotEmpty(TestSeasons.AllChoices);
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s1_400days")]
    public void ForSeason_ReturnsChoices(string seasonKey)
    {
        var choices = TestSeasons.ChoicesFor(seasonKey).ToList();
        Assert.NotEmpty(choices);
        Assert.All(choices, c => Assert.Equal(seasonKey, c.SeasonKey));
    }

    [Theory]
    [InlineData("s2", 25)]
    [InlineData("s3", 24)]
    [InlineData("s4", 16)]
    [InlineData("michonne", 36)]
    public void ForSeason_GameSourcedSeasons_HaveChoices(string seasonKey, int minCount)
    {
        var choices = TestSeasons.ChoicesFor(seasonKey).ToList();
        Assert.True(choices.Count >= minCount,
            $"{seasonKey}: expected >= {minCount}, got {choices.Count}");
        Assert.All(choices, c => Assert.Equal(seasonKey, c.SeasonKey));
    }

    [Fact]
    public void AllChoices_HaveValidStructure()
    {
        foreach (var choice in TestSeasons.AllChoices)
        {
            Assert.NotEmpty(choice.Description);
            Assert.NotEmpty(choice.ChoiceKey);
            Assert.True(choice.Options.Length >= 2, $"Choice '{choice.Description}' needs at least 2 options");

            foreach (var opt in choice.Options)
            {
                Assert.NotEmpty(opt.Label);
                Assert.NotEmpty(opt.Value);
            }
        }
    }

    [Fact]
    public void Registry_HasAllSeasons()
    {
        Assert.Equal(6, TestSeasons.Registry.All.Count);
    }

    [Fact]
    public void Registry_Get_Works()
    {
        var s1 = TestSeasons.Registry.Get("s1");
        Assert.NotNull(s1);
        Assert.Equal("Season 1", s1.Name);
        Assert.Equal(5, s1.Episodes.Count);
    }

    [Fact]
    public void Registry_Get_CaseInsensitive()
    {
        Assert.NotNull(TestSeasons.Registry.Get("S1"));
        Assert.NotNull(TestSeasons.Registry.Get("MICHONNE"));
    }

}
