using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Common.Tests.Extensions;

public class CreateSaveTests
{
    [Fact]
    public void CreateForSeason_HasPrePopulatedChoices()
    {
        var registry = TestSeasons.Registry;
        var slot = registry.CreateSave("s1", 3, "wd1_saveslot1.bundle");
        var accessor = registry.Get("s1")!.CreateChoiceAccessor(slot)!;

        var earlierChoices = TestSeasons.ChoicesFor("s1")
            .Where(c => c.Episode < 3)
            .ToList();
        Assert.NotEmpty(earlierChoices);

        foreach (var choice in earlierChoices)
        {
            var val = accessor.GetChoiceValue(choice.ChoiceKey);
            Assert.NotNull(val);
            Assert.Equal(choice.Options[0].Value, val);
        }
    }

    [Fact]
    public void CreateForSeason_DoesNotIncludeLaterEpisodes()
    {
        var registry = TestSeasons.Registry;
        var slot = registry.CreateSave("s1", 2, "wd1_saveslot1.bundle");
        var accessor = registry.Get("s1")!.CreateChoiceAccessor(slot)!;

        var ep2Choice = TestSeasons.ChoicesFor("s1", 2).FirstOrDefault();
        if (ep2Choice != null)
        {
            Assert.Null(accessor.GetChoiceValue(ep2Choice.ChoiceKey));
        }
    }

    [Fact]
    public void CreateForSeason_S1_RoundTrips()
    {
        var registry = TestSeasons.Registry;
        var handler = registry.Get("s1")!;
        var slot = registry.CreateSave("s1", 4, "wd1_saveslot1.bundle");

        var reparsed = BundleReader.Read(BundleWriter.Write(slot), "wd1_saveslot1.bundle");

        var original = handler.CreateChoiceAccessor(slot)!;
        var reloaded = handler.CreateChoiceAccessor(reparsed)!;
        foreach (var choice in TestSeasons.ChoicesFor("s1").Where(c => c.Episode < 4))
        {
            Assert.Equal(original.GetChoiceValue(choice.ChoiceKey), reloaded.GetChoiceValue(choice.ChoiceKey));
        }
    }

    [Fact]
    public void CreateForSeason_S2_RoundTripsTheSeason1Decisions()
    {
        var handler = TestSeasons.Registry.Get("s2")!;
        var slot = TestSeasons.Registry.CreateSave("s2", 3, "wd2_saveslot1.bundle");
        var reparsed = BundleReader.Read(BundleWriter.Write(slot), "wd2_saveslot1.bundle");

        var original = handler.CreateChoiceAccessor(slot)!;
        var reloaded = handler.CreateChoiceAccessor(reparsed)!;
        var season1 = TestSeasons.ChoicesFor("s1").ToList();
        Assert.All(season1, choice => Assert.NotNull(original.GetChoiceValue(choice.ChoiceKey)));
        Assert.All(season1, choice => Assert.Equal(original.GetChoiceValue(choice.ChoiceKey), reloaded.GetChoiceValue(choice.ChoiceKey)));
    }

    [Fact]
    public void CreateForSeason_S4_RoundTrips()
    {
        var slot = TestSeasons.Registry.CreateSave("s4", 2, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.Null(reparsed.ChoiceStats);
        Assert.Null(reparsed.Choices);
        Assert.Equal(2, reparsed.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
    }

    [Fact]
    public void CreateForSeason_Michonne_HasMetadataOnly()
    {
        var slot = TestSeasons.Registry.CreateSave("michonne", 1, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.NotNull(reparsed.Metadata);
        Assert.Null(reparsed.Choices);
        Assert.Null(reparsed.ChoiceStats);
    }
    [Fact]
    public void NewSave_S3_HasMetadataOnly()
    {
        var slot = TestSeasons.Registry.CreateSave("s3", 1, "test_s3.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Null(slot.ChoiceStats);
        Assert.Single(slot.Files);

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "test_s3.bundle");
        Assert.NotNull(reloaded.Metadata);
        Assert.Null(reloaded.Choices);
    }

    [Fact]
    public void NewSave_Michonne_HasMetadataOnly()
    {
        var slot = TestSeasons.Registry.CreateSave("michonne", 1, "test_michonne.bundle");
        Assert.NotNull(slot.Metadata);
        Assert.Null(slot.Choices);
        Assert.Single(slot.Files);
    }

    [Fact]
    public void NewSave_S1S2_HaveChoices()
    {
        foreach (var season in new[] { "s1", "s1_400days", "s2" })
        {
            var slot = TestSeasons.Registry.CreateSave(season, 1, $"test_{season}.bundle");
            Assert.NotNull(slot.Choices);

            var written = BundleWriter.Write(slot);
            var reloaded = BundleReader.Read(written, $"test_{season}.bundle");
            Assert.NotNull(reloaded.Choices);
        }
    }

    [Fact]
    public void S2_CreateEditSaveReload()
    {
        var handler = TestSeasons.Registry.Get("s2")!;
        var choice = TestSeasons.ChoicesFor("s1").First();
        var slot = TestSeasons.Registry.CreateSave("s2", 5, "wd2_saveslot1.bundle");

        handler.CreateChoiceAccessor(slot)!.ApplyChoice(choice, 1);
        var reloaded = BundleReader.Read(BundleWriter.Write(slot), "wd2_saveslot1.bundle");
        Assert.Equal(1, handler.CreateChoiceAccessor(reloaded)!.DetectCurrentChoice(choice));

        handler.CreateChoiceAccessor(reloaded)!.ApplyChoice(choice, 0);
        var reloadedAgain = BundleReader.Read(BundleWriter.Write(reloaded), "wd2_saveslot1.bundle");
        Assert.Equal(0, handler.CreateChoiceAccessor(reloadedAgain)!.DetectCurrentChoice(choice));
    }
}
