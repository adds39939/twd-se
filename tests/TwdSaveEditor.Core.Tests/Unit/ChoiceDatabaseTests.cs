using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Extensions;

namespace TwdSaveEditor.Core.Tests.Unit;

public class ChoiceDatabaseTests
{
    private static readonly ChoiceDefinition DougCarley = new()
    {
        SeasonKey = "test",
        Episode = 1,
        Description = "Doug or Carley",
        ChoiceKey = "dougcarley_saved",
        Options =
        [
            new ChoiceOption { Label = "Doug", Value = "doug" },
            new ChoiceOption { Label = "Carley", Value = "carley" },
        ],
    };

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
    [InlineData("michonne", 15)]
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
    public void DetectCurrentChoice_MatchesChoice()
    {
        var ps = new PropertySet();
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = new TypeGroup(typeSymbol);
        var raw = ChoicesContainer.Serialize([
            ("dougcarley_saved - carley", true)
        ]);
        group.Properties.Add(new Property(
            Symbol.FromString("episode_1_choices"),
            new RawBytesValue(raw, typeSymbol)));
        ps.TypeGroups.Add(group);

        var accessor = new SaveAccessor(ps);

        var choice = DougCarley;

        var detected = accessor.DetectCurrentChoice(choice);
        Assert.Equal(1, detected);
    }

    [Fact]
    public void ApplyChoice_WritesCorrectValues()
    {
        var ps = new PropertySet();
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = new TypeGroup(typeSymbol);
        var raw = ChoicesContainer.Serialize([
            ("dougcarley_saved - carley", true)
        ]);
        group.Properties.Add(new Property(
            Symbol.FromString("episode_1_choices"),
            new RawBytesValue(raw, typeSymbol)));
        ps.TypeGroups.Add(group);

        var accessor = new SaveAccessor(ps);

        var choice = DougCarley;

        accessor.ApplyChoice(choice, 0);
        Assert.Equal("doug", accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void DetectCurrentChoice_ReturnsNegativeOne_WhenNoData()
    {
        var ps = new PropertySet();
        var accessor = new SaveAccessor(ps);

        var choice = TestSeasons.AllChoices.First();
        Assert.Equal(-1, accessor.DetectCurrentChoice(choice));
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

    [Theory]
    [InlineData("s1", "WalkingDead101")]
    [InlineData("s1_400days", "WalkingDead104")]
    [InlineData("s2", "WalkingDead201")]
    [InlineData("s3", "WalkingDead301")]
    [InlineData("s4", "WalkingDead401")]
    [InlineData("michonne", "Michonne101")]
    public void Season_HasScenesForItsEpisodes(string seasonKey, string episodeId)
    {
        var season = TestSeasons.Registry.Get(seasonKey)!;
        Assert.NotEmpty(season.GetScenes(episodeId));
        Assert.Equal(season.GetScenes(episodeId), TestSeasons.Registry.GetScenes(episodeId));
    }

    [Fact]
    public void Season_HasNoScenesForOtherSeasonsEpisodes()
    {
        Assert.Empty(TestSeasons.Registry.Get("s1")!.GetScenes("WalkingDead201"));
        Assert.Empty(TestSeasons.Registry.GetScenes("NoSuchEpisode"));
    }
}
