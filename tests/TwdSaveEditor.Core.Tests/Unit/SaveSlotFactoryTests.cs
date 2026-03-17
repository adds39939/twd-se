using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Hashing;

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
        Assert.Equal(0x4D535636U, slot.OuterHeader.Magic); // MSV6
        Assert.Equal(2, slot.OuterHeader.VersionEntries.Count);
        Assert.Equal(2, slot.FileTable.Count);
        Assert.Equal("metadata_slot.p", slot.FileTable[0].Name);
        Assert.Equal("choices.prop", slot.FileTable[1].Name);
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.Choices);
        Assert.NotNull(slot.RawMetadataFile);
        Assert.NotNull(slot.RawChoicesFile);
        Assert.NotNull(slot.RawInnerFiles);
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

        // Re-parse
        var reparsed = BundleReader.Read(bytes, "test.bundle");
        Assert.NotNull(reparsed.Metadata);
        Assert.NotNull(reparsed.Choices);
        Assert.Equal(2, reparsed.FileTable.Count);
    }

    [Fact]
    public void CreateForSeason_HasPrePopulatedChoices()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "s1", 3, "test.bundle");
        var accessor = new SaveAccessor(slot.Choices!);

        var allChoices = accessor.GetAllChoices();
        Assert.NotEmpty(allChoices);

        // S1 episodes 1-3 choices should be present
        var s1Ep1to3 = ChoiceDatabase.ForSeason("s1")
            .Where(c => c.Episode <= 3)
            .ToList();

        foreach (var choice in s1Ep1to3)
        {
            var val = accessor.GetChoiceValue(choice.ChoiceKey);
            Assert.NotNull(val);
            // Default is first option
            Assert.Equal(choice.Options[0].Value, val);
        }
    }

    [Fact]
    public void CreateForSeason_DoesNotIncludeLaterEpisodes()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "s1", 1, "test.bundle");
        var accessor = new SaveAccessor(slot.Choices!);

        // Ep 2+ choices should NOT be present
        var ep2Choice = ChoiceDatabase.ForEpisode("s1", 2).FirstOrDefault();
        if (ep2Choice != null)
        {
            Assert.Null(accessor.GetChoiceValue(ep2Choice.ChoiceKey));
        }
    }

    [Theory]
    [InlineData("s1", 1)]
    [InlineData("s2", 3)]
    public void CreateForSeason_S1S2_RoundTrips(string seasonKey, int episode)
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), seasonKey, episode, "test.bundle");
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
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "s4", 2, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.NotNull(reparsed.ChoiceStats);
        Assert.Null(reparsed.Choices);
    }

    [Fact]
    public void CreateForSeason_Michonne_HasMetadataOnly()
    {
        var slot = SaveSlotFactory.CreateForSeason(CreateRegistry(), "michonne", 1, "test.bundle");
        var bytes = BundleWriter.Write(slot);
        var reparsed = BundleReader.Read(bytes, "test.bundle");

        Assert.NotNull(reparsed.Metadata);
        Assert.Null(reparsed.Choices);
        Assert.Null(reparsed.ChoiceStats);
    }
}
