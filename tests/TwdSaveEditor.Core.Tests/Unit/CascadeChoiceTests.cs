using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Handlers;

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
        var s1 = Registry.CreateSave("s1", 1, "wd1_saveslot1.bundle");
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");

        var s1Accessor = new SaveAccessor(s1.Choices!, s1.Metadata);
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        s1Accessor.SetChoiceValue("dougcarley_saved", "doug");

        s2Accessor.SetChoiceValue("dougcarley_saved", "doug");

        Assert.Equal("doug", s2Accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void S1ChoiceChange_DoesNotAffectS2_WhenNotCascaded()
    {
        var s1 = Registry.CreateSave("s1", 1, "wd1_saveslot1.bundle");
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");

        var s1Accessor = new SaveAccessor(s1.Choices!, s1.Metadata);
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        var s2Before = s2Accessor.GetChoiceValue("dougcarley_saved");

        s1Accessor.SetChoiceValue("dougcarley_saved", "doug");

        Assert.Equal(s2Before, s2Accessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void CascadedChoice_SurvivesRoundTrip()
    {
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");
        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

        s2Accessor.SetChoiceValue("dougcarley_saved", "carley");

        var written = BundleWriter.Write(s2);
        var reloaded = BundleReader.Read(written, "wd2_saveslot1.bundle");
        var reloadedAccessor = new SaveAccessor(reloaded.Choices!, reloaded.Metadata);

        Assert.Equal("carley", reloadedAccessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void MultipleS1Choices_AllCascadeToS2()
    {
        var s1 = Registry.CreateSave("s1", 2, "wd1_saveslot1.bundle");
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");

        var s2Accessor = new SaveAccessor(s2.Choices!, s2.Metadata);

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
