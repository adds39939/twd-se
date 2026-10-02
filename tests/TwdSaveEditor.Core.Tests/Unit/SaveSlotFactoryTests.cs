using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Tests.Support;
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

public class SaveSlotFactoryTests
{
    private static ISeasonRegistry CreateRegistry() => new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new S3Handler(), new S4Handler(), new MichonneHandler(),
    ]);

    [Fact]
    public void CreateBlank_HasValidStructure()
    {
        var slot = SaveSlotFactory.CreateBlank("test.bundle");

        Assert.NotNull(slot.OuterHeader);
        Assert.Equal(0x4D535636U, slot.OuterHeader.Magic);
        Assert.Equal(2, slot.OuterHeader.VersionEntries.Count);
        Assert.Equal(2, slot.Files.Count);
        Assert.Equal("metadata_slot.p", slot.Files[0].Name);
        Assert.Equal("choices.prop", slot.Files[1].Name);
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.Choices);
    }

    [Fact]
    public void CreateBlank_MetadataHasCorrectFlags()
    {
        var slot = SaveSlotFactory.CreateBlank("test.bundle");

        Assert.Equal(2U, slot.Metadata!.Version);
        Assert.Equal(0x100U, slot.Metadata.Flags);
    }

    [Fact]
    public void CreateBlank_ChoicesHasCorrectFlags()
    {
        var slot = SaveSlotFactory.CreateBlank("test.bundle");

        Assert.Equal(2U, slot.Choices!.Version);
        Assert.Equal(0U, slot.Choices.Flags);
    }

    [Fact]
    public void CreateBlank_RoundTripsThroughBundleWriter()
    {
        var slot = SaveSlotFactory.CreateBlank("test.bundle");
        var bytes = BundleWriter.Write(slot);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        var reparsed = BundleReader.Read(bytes, "test.bundle");
        Assert.NotNull(reparsed.Metadata);
        Assert.NotNull(reparsed.Choices);
        Assert.Equal(2, reparsed.Files.Count);
    }

    [Fact]
    public void CreateForSeason_HasPrePopulatedChoices()
    {
        var registry = CreateRegistry();
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
        var registry = CreateRegistry();
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
        var registry = CreateRegistry();
        var handler = registry.Get("s1")!;
        var slot = registry.CreateSave("s1", 4, "wd1_saveslot1.bundle");

        var reparsed = BundleReader.Read(BundleWriter.Write(slot), "wd1_saveslot1.bundle");

        var original = handler.CreateChoiceAccessor(slot)!;
        var reloaded = handler.CreateChoiceAccessor(reparsed)!;
        foreach (var choice in TestSeasons.ChoicesFor("s1").Where(c => c.Episode < 4))
            Assert.Equal(original.GetChoiceValue(choice.ChoiceKey), reloaded.GetChoiceValue(choice.ChoiceKey));
    }

    [Theory]
    [InlineData("s2", 3)]
    public void CreateForSeason_S2_RoundTrips(string seasonKey, int episode)
    {
        var slot = CreateRegistry().CreateSave(seasonKey, episode, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        var origAccessor = new SaveAccessor(slot.Choices!);
        var newAccessor = new SaveAccessor(reparsed.Choices!);

        var origChoices = origAccessor.GetAllChoices();
        var newChoices = newAccessor.GetAllChoices();
        Assert.Equal(origChoices.Count, newChoices.Count);

        foreach (var (key, value) in origChoices)
            Assert.Equal(value, newAccessor.GetChoiceValue(key)!);
    }

    [Fact]
    public void CreateForSeason_S4_RoundTrips()
    {
        var slot = CreateRegistry().CreateSave("s4", 2, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.Null(reparsed.ChoiceStats);
        Assert.Null(reparsed.Choices);
        Assert.Equal(2, reparsed.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
    }

    [Fact]
    public void CreateForSeason_Michonne_HasMetadataOnly()
    {
        var slot = CreateRegistry().CreateSave("michonne", 1, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.NotNull(reparsed.Metadata);
        Assert.Null(reparsed.Choices);
        Assert.Null(reparsed.ChoiceStats);
    }
}
