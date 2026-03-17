using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

public class ChoiceDatabaseTests
{
    [Fact]
    public void AllChoices_HasEntries()
    {
        Assert.NotEmpty(ChoiceDatabase.AllChoices);
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s1_400days")]
    public void ForSeason_ReturnsChoices(string seasonKey)
    {
        var choices = ChoiceDatabase.ForSeason(seasonKey).ToList();
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
        var choices = ChoiceDatabase.ForSeason(seasonKey).ToList();
        Assert.True(choices.Count >= minCount,
            $"{seasonKey}: expected >= {minCount}, got {choices.Count}");
        Assert.All(choices, c => Assert.Equal(seasonKey, c.SeasonKey));
    }

    [Fact]
    public void AllChoices_HaveValidStructure()
    {
        foreach (var choice in ChoiceDatabase.AllChoices)
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
        // Build a choices PropertySet with a real entry
        var ps = new PropertySet();
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = new TypeGroup(typeSymbol);
        var raw = SaveAccessor.SerializeStringBoolArray([
            ("dougcarley_saved - carley", true)
        ]);
        group.Properties.Add(new Property(
            Symbol.FromString("episode_1_choices"),
            new RawBytesValue(raw, typeSymbol)));
        ps.TypeGroups.Add(group);

        var accessor = new SaveAccessor(ps);

        var choice = ChoiceDatabase.ForEpisode("s1", 1)
            .First(c => c.ChoiceKey == "dougcarley_saved");

        var detected = accessor.DetectCurrentChoice(choice);
        Assert.Equal(1, detected); // "Saved Carley" is option 1
    }

    [Fact]
    public void ApplyChoice_WritesCorrectValues()
    {
        var ps = new PropertySet();
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = new TypeGroup(typeSymbol);
        var raw = SaveAccessor.SerializeStringBoolArray([
            ("dougcarley_saved - carley", true)
        ]);
        group.Properties.Add(new Property(
            Symbol.FromString("episode_1_choices"),
            new RawBytesValue(raw, typeSymbol)));
        ps.TypeGroups.Add(group);

        var accessor = new SaveAccessor(ps);

        var choice = ChoiceDatabase.ForEpisode("s1", 1)
            .First(c => c.ChoiceKey == "dougcarley_saved");

        // Apply "Saved Doug" (option 0)
        accessor.ApplyChoice(choice, 0);
        Assert.Equal("doug", accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void DetectCurrentChoice_ReturnsNegativeOne_WhenNoData()
    {
        var ps = new PropertySet();
        var accessor = new SaveAccessor(ps);

        var choice = ChoiceDatabase.AllChoices.First();
        Assert.Equal(-1, accessor.DetectCurrentChoice(choice));
    }

    [Fact]
    public void SeasonInfo_HasAllSeasons()
    {
        Assert.Equal(6, SeasonInfo.Seasons.Length);
    }

    [Fact]
    public void SeasonInfo_FindSeason_Works()
    {
        var s1 = SeasonInfo.FindSeason("s1");
        Assert.NotNull(s1);
        Assert.Equal("Season 1", s1.Name);
        Assert.Equal(5, s1.EpisodeCount);
    }

    [Fact]
    public void SeasonInfo_FindSeason_CaseInsensitive()
    {
        Assert.NotNull(SeasonInfo.FindSeason("S1"));
        Assert.NotNull(SeasonInfo.FindSeason("MICHONNE"));
    }
}
