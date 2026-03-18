using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

/// <summary>
/// Tests against the live S1 save from the actual game installation.
/// </summary>
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

        // A fresh S1 save may or may not have choices.prop depending on progress
        if (slot.Choices != null)
        {
            var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
            var choices = accessor.GetAllChoices();
            // Just verify it parses without error
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
        if (slot.Choices == null) return; // Can't edit choices if none exist yet

        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);

        // Try setting a choice
        accessor.SetChoiceValue("dougcarley_saved", "carley");
        Assert.Equal("carley", accessor.GetChoiceValue("dougcarley_saved"));

        // Round-trip
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

        // Outer header should have 2 version entries matching our constants
        Assert.Equal(2, slot.OuterHeader.VersionEntries.Count);
        Assert.Equal(0xE09B099B8076C147UL, slot.OuterHeader.VersionEntries[0].TypeCrc);
        Assert.Equal(0x004F023463D89FB0UL, slot.OuterHeader.VersionEntries[1].TypeCrc);
    }
}
