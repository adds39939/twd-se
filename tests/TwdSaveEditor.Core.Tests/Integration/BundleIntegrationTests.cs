using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Season.Base.Accessors;

namespace TwdSaveEditor.Core.Tests.Integration;

public class BundleIntegrationTests
{
    [Fact]
    public void BundleReader_ParsesRealSaveFile()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(path);

        Assert.NotNull(slot);
        Assert.NotEmpty(slot.Files);
        Assert.NotNull(slot.Metadata);
        Assert.NotNull(slot.Choices);
        Assert.True(slot.Choices.TypeGroups.Count > 0, "Choices should have type groups");
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

    [Fact]
    public void BundleWriter_RoundTripsWithoutCrash()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var slot = BundleReader.Read(path);

        var output = BundleWriter.Write(slot);
        Assert.NotNull(output);
        Assert.True(output.Length > 0);

        var reparsed = BundleReader.Read(output, "test.bundle");
        Assert.NotNull(reparsed.Metadata);
        Assert.NotNull(reparsed.Choices);

        var origAccessor = new SaveAccessor(slot.Choices!, slot.Metadata);
        var newAccessor = new SaveAccessor(reparsed.Choices!, reparsed.Metadata);

        var origChoices = origAccessor.GetAllChoices();
        var newChoices = newAccessor.GetAllChoices();
        Assert.Equal(origChoices.Count, newChoices.Count);
    }
}
