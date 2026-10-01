using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Core.Tests.Integration;

public class LiveSaveTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new S3Handler(), new S4Handler(), new MichonneHandler(),
    ]);

    [Fact]
    public void LiveS1Save_ParsesSuccessfully()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);
        slot.DetectedSeasonKey = Registry.DetectFromFileName(path)?.SeasonKey;

        Assert.NotNull(slot.Metadata);
        Assert.Equal("s1", slot.DetectedSeasonKey);
        Assert.NotNull(slot.OuterHeader);
        Assert.Equal(MetaStreamHeader.MagicMsv6, slot.OuterHeader.Magic);
    }

    [Fact]
    public void LiveS1Save_HasValidMetadata()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);

        Assert.NotNull(slot.Metadata);
        Assert.Equal(2u, slot.Metadata.Version);
        Assert.Equal(0x100u, slot.Metadata.Flags);
        Assert.True(slot.Metadata.AllProperties.Any(), "Metadata should have properties");
    }

    [Fact]
    public void LiveS1Save_HasChoices()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);

        if (slot.Choices != null)
        {
            var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
            var choices = accessor.GetAllChoices();
            Assert.NotNull(choices);
        }
    }

    [Fact]
    public void LiveS1Save_RoundTrips()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

        Assert.NotNull(reloaded.Metadata);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());

        Assert.Equal(slot.FileTable.Count, reloaded.FileTable.Count);
        for (int i = 0; i < slot.FileTable.Count; i++)
        {
            Assert.Equal(slot.FileTable[i].Name, reloaded.FileTable[i].Name);
            Assert.Equal(slot.FileTable[i].Hash1, reloaded.FileTable[i].Hash1);
            Assert.Equal(slot.FileTable[i].Hash2, reloaded.FileTable[i].Hash2);
        }
    }

    [Fact]
    public void LiveS1Save_CanEditAndRoundTrip()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);
        if (slot.Choices == null) return;

        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);

        accessor.SetChoiceValue("dougcarley_saved", "carley");
        Assert.Equal("carley", accessor.GetChoiceValue("dougcarley_saved"));

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

        Assert.NotNull(reloaded.Choices);
        var reloadedAccessor = new SaveAccessor(reloaded.Choices, reloaded.Metadata);
        Assert.Equal("carley", reloadedAccessor.GetChoiceValue("dougcarley_saved"));
    }

    [Fact]
    public void LiveS1Autosave_ParsesWithoutCrash()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);
        Assert.NotNull(slot.OuterHeader);
        Assert.NotNull(slot.Metadata);
    }

    [Fact]
    public void LiveS1Save_VersionEntriesMatchExpected()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path)) return;

        var slot = BundleReader.Read(path);

        Assert.Equal(2, slot.OuterHeader.VersionEntries.Count);
        Assert.Equal(0xE09B099B8076C147UL, slot.OuterHeader.VersionEntries[0].TypeCrc);
        Assert.Equal(0x004F023463D89FB0UL, slot.OuterHeader.VersionEntries[1].TypeCrc);
    }
}
