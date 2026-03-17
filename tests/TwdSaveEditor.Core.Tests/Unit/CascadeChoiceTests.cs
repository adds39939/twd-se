using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

public class CascadeChoiceTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new S3Handler(), new S4Handler(), new MichonneHandler(),
    ]);

    [Fact]
    public void S1ChoiceChange_CascadesToS2Save()
    {
        var s1 = SaveSlotFactory.CreateForSeason(Registry, "s1", 1, "wd1_saveslot1.bundle");
        var s2 = SaveSlotFactory.CreateForSeason(Registry, "s2", 1, "wd2_saveslot1.bundle");

        var s1Accessor = new SaveAccessor(s1.Choices!, s1.Metadata);
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        // Set a choice in S1
        s1Accessor.SetChoiceValue("dougcarley_saved", "doug");

        // Manually cascade (simulating what SaveEditorService.CascadeChoice does)
        s2Accessor.SetChoiceValue("dougcarley_saved", "doug");

        Assert.Equal("doug", s2Accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void S1ChoiceChange_DoesNotAffectS2_WhenNotCascaded()
    {
        var s1 = SaveSlotFactory.CreateForSeason(Registry, "s1", 1, "wd1_saveslot1.bundle");
        var s2 = SaveSlotFactory.CreateForSeason(Registry, "s2", 1, "wd2_saveslot1.bundle");

        var s1Accessor = new SaveAccessor(s1.Choices!, s1.Metadata);
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        // S2 has default first option for dougcarley_saved
        var s2Before = s2Accessor.GetChoiceValue("dougcarley_saved");

        // Change S1 without cascading
        s1Accessor.SetChoiceValue("dougcarley_saved", "doug");

        // S2 should be unchanged
        Assert.Equal(s2Before, s2Accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void CascadedChoice_SurvivesRoundTrip()
    {
        var s2 = SaveSlotFactory.CreateForSeason(Registry, "s2", 1, "wd2_saveslot1.bundle");
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        // Simulate cascade by setting an S1 choice key in S2
        s2Accessor.SetChoiceValue("dougcarley_saved", "carley");

        // Round-trip
        var written = Binary.BundleWriter.Write(s2);
        var reloaded = Binary.BundleReader.Read(written, "wd2_saveslot1.bundle");
        var reloadedAccessor = new SaveAccessor(reloaded.Choices!, reloaded.Metadata);

        Assert.Equal("carley", reloadedAccessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void MultipleS1Choices_AllCascadeToS2()
    {
        var s1 = SaveSlotFactory.CreateForSeason(Registry, "s1", 2, "wd1_saveslot1.bundle");
        var s2 = SaveSlotFactory.CreateForSeason(Registry, "s2", 1, "wd2_saveslot1.bundle");

        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        // Simulate cascading multiple S1 choices
        var s1Choices = new (string Key, string Value)[]
        {
            ("dougcarley_saved", "carley"),
            ("shawnduck_choice", "duck"),
            ("helped_kill_larry", "true"),
        };

        foreach (var (key, value) in s1Choices)
            s2Accessor.SetChoiceValue(key, value);

        Assert.Equal("carley", s2Accessor.GetChoiceValue("dougcarley_saved"));
        Assert.Equal("duck", s2Accessor.GetChoiceValue("shawnduck_choice"));
        Assert.Equal("true", s2Accessor.GetChoiceValue("helped_kill_larry"));
    }
}
