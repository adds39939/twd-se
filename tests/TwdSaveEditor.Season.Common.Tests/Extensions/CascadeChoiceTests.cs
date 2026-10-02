using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Season.Common.Extensions;

namespace TwdSaveEditor.Season.Common.Tests.Extensions;

public class CascadeChoiceTests
{
    private static readonly ISeasonRegistry Registry = TestSeasons.Registry;

    [Fact]
    public void S1Choice_IsStoredAsPersistentKeyInS2Save()
    {
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");
        var s2Accessor = Registry.Get("s2")!.CreateChoiceAccessor(s2)!;

        s2Accessor.SetChoiceValue("DougCarley Saved", "Doug");

        Assert.Equal("doug", s2Accessor.GetChoiceValue("DougCarley Saved"));
        Assert.Equal("doug", s2.Choices!.GetString("DougCarley Saved"));
    }

    [Fact]
    public void S1ChoiceChange_DoesNotAffectS2_WhenNotCascaded()
    {
        var s1 = Registry.CreateSave("s1", 2, "wd1_saveslot1.bundle");
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");

        var s1Accessor = Registry.Get("s1")!.CreateChoiceAccessor(s1)!;
        var s2Accessor = Registry.Get("s2")!.CreateChoiceAccessor(s2)!;

        s1Accessor.SetChoiceValue("DougCarley Saved", "doug");

        Assert.Equal("carley", s2Accessor.GetChoiceValue("DougCarley Saved"));
    }

    [Fact]
    public void CascadedChoice_SurvivesRoundTrip()
    {
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");
        Registry.Get("s2")!.CreateChoiceAccessor(s2)!.SetChoiceValue("DougCarley Saved", "carley");

        var reloaded = BundleReader.Read(BundleWriter.Write(s2), "wd2_saveslot1.bundle");

        Assert.Equal("carley", Registry.Get("s2")!.CreateChoiceAccessor(reloaded)!.GetChoiceValue("DougCarley Saved"));
    }

    [Fact]
    public void S2OwnChoices_GoToTheEventLogAndNotTheSeason1Values()
    {
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");
        var s2Accessor = Registry.Get("s2")!.CreateChoiceAccessor(s2)!;

        s2Accessor.SetChoiceValue("Episode 205 - Shot Kenny", "shot_kenny");

        Assert.Equal("shot_kenny", s2Accessor.GetChoiceValue("Episode 205 - Shot Kenny"));
        Assert.Null(s2.Choices!.GetString("Episode 205 - Shot Kenny"));
        Assert.Single(s2.EventLog!.Events, entry => entry.DialogNode != null);
    }
}
