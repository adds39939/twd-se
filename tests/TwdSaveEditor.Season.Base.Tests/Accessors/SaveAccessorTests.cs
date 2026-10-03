using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Base.Tests.Accessors;

public class SaveAccessorTests
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

    private static (PropertySet ps, SaveAccessor accessor) CreateWithChoices(
        params (string str, bool boolVal)[] entries)
    {
        var ps = new PropertySet();
        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = new TypeGroup(typeSymbol);
        var raw = ChoicesContainer.Serialize(entries.ToList());
        group.Properties.Add(new Property(
            Symbol.FromString("episode_1_choices"),
            new RawBytesValue(raw, typeSymbol)));
        ps.TypeGroups.Add(group);
        var accessor = new SaveAccessor(ps);
        return (ps, accessor);
    }

    [Fact]
    public void GetChoiceValue_FindsEntry()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true),
            ("lied_to_hershel - true", true));

        Assert.Equal("carley", accessor.GetChoiceValue("dougcarley_saved"));
        Assert.Equal("true", accessor.GetChoiceValue("lied_to_hershel"));
    }

    [Fact]
    public void GetChoiceValue_ReturnsNull_WhenMissing()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true));

        Assert.Null(accessor.GetChoiceValue("nonexistent"));
    }

    [Fact]
    public void SetChoiceValue_UpdatesExistingEntry()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true),
            ("lied_to_hershel - true", true));

        accessor.SetChoiceValue("dougcarley_saved", "doug");
        Assert.Equal("doug", accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void GetAllChoices_ReturnsAll()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true),
            ("lied_to_hershel - true", true),
            ("shawnduck_choice - duck", true));

        var all = accessor.GetAllChoices();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, e => e.key == "dougcarley_saved" && e.value == "carley");
        Assert.Contains(all, e => e.key == "shawnduck_choice" && e.value == "duck");
    }

    [Fact]
    public void DetectCurrentChoice_MatchesCorrectOption()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true));

        var choice = DougCarley;

        var detected = accessor.DetectCurrentChoice(choice);
        Assert.Equal(1, detected);
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
    public void ApplyChoice_ThenDetect_RoundTrips()
    {
        var (_, accessor) = CreateWithChoices(
            ("dougcarley_saved - carley", true));

        var choice = DougCarley;

        accessor.ApplyChoice(choice, 0);
        Assert.Equal(0, accessor.DetectCurrentChoice(choice));

        accessor.ApplyChoice(choice, 1);
        Assert.Equal(1, accessor.DetectCurrentChoice(choice));
    }

    [Fact]
    public void ParseStringBoolArray_RoundTrips()
    {
        var original = new List<(string str, bool boolVal)>
        {
            ("test - value", true),
            ("another - thing", false),
        };

        var bytes = ChoicesContainer.Serialize(original);
        var parsed = ChoicesContainer.Parse(bytes);

        Assert.Equal(original.Count, parsed.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].str, parsed[i].str);
            Assert.Equal(original[i].boolVal, parsed[i].boolVal);
        }
    }

    [Fact]
    public void GetMetadataInt_ReadsFromMetadata()
    {
        var metadata = new PropertySet();
        var intGroup = new TypeGroup(Symbol.FromString("int32"));
        intGroup.Properties.Add(new Property(
            Symbol.FromString("mCurrentEpisode"),
            new IntValue(3)));
        metadata.TypeGroups.Add(intGroup);

        var choices = new PropertySet();
        var accessor = new SaveAccessor(choices, metadata);

        Assert.Equal(3, accessor.GetMetadataInt("mCurrentEpisode"));
    }

    [Fact]
    public void GetMetadataString_ReadsFromMetadata()
    {
        var metadata = new PropertySet();
        var strGroup = new TypeGroup(Symbol.FromString("String"));
        strGroup.Properties.Add(new Property(
            Symbol.FromString("mActiveSeason"),
            new StringValue("s2")));
        metadata.TypeGroups.Add(strGroup);

        var choices = new PropertySet();
        var accessor = new SaveAccessor(choices, metadata);

        Assert.Equal("s2", accessor.GetMetadataString("mActiveSeason"));
    }
    [Fact]
    public void SaveAccessor_WorksWithNullChoices()
    {
        var accessor = new SaveAccessor(null, null);
        Assert.False(accessor.HasChoices);
        Assert.Null(accessor.GetChoiceValue("test_key"));
        Assert.Empty(accessor.GetAllChoices());
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetChoiceValue("test_key", "test_value"));
    }

    [Fact]
    public void S2_SaveAccessor_DetectsChoicesFromRealSave()
    {
        var file = TestDataHelper.GetPath("S2", "wd2_saveslot1.bundle");
        var slot = BundleReader.Read(file);
        Assert.NotNull(slot.Choices);

        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
        var allChoices = accessor.GetAllChoices();
        Assert.True(allChoices.Count > 0, "Expected S2 imported choices");
    }

    [Fact]
    public void SaveAccessor_ReadsChoicesFromRealSave()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(path);
        var accessor = new SaveAccessor(slot.Choices!, slot.Metadata);

        var allChoices = accessor.GetAllChoices();

        Assert.Contains(("dougcarley_saved", "carley"), allChoices);
        Assert.Contains(("shawnduck_choice", "duck"), allChoices);
        Assert.Contains(("weapon_choice", "inventory_-_spike_remover"), allChoices);
    }

}
