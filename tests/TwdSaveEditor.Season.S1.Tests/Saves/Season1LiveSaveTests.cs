using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Season.Base.Accessors;

namespace TwdSaveEditor.Season.S1.Tests.Saves;

public class LiveSaveTests
{
    private static readonly ISeasonRegistry Registry = TestSeasons.Registry;

    [Fact]
    public void LiveS1Save_ParsesSuccessfully()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path))
        {
            return;
        }

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
        if (!File.Exists(path))
        {
            return;
        }

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
        if (!File.Exists(path))
        {
            return;
        }

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
        if (!File.Exists(path))
        {
            return;
        }

        var slot = BundleReader.Read(path);
        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

        Assert.NotNull(reloaded.Metadata);
        Assert.Equal(
            slot.Metadata!.AllProperties.Count(),
            reloaded.Metadata!.AllProperties.Count());

        Assert.Equal(slot.Files.Count, reloaded.Files.Count);
        for (int i = 0; i < slot.Files.Count; i++)
        {
            Assert.Equal(slot.Files[i].Name, reloaded.Files[i].Name);
            Assert.Equal(slot.Files[i].NameSymbol, reloaded.Files[i].NameSymbol);
            Assert.Equal(slot.Files[i].TypeSymbol, reloaded.Files[i].TypeSymbol);
        }
    }

    [Fact]
    public void LiveS1Save_CanEditAndRoundTrip()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path))
        {
            return;
        }

        var slot = BundleReader.Read(path);
        var handler = Registry.Get("s1")!;
        var accessor = handler.CreateChoiceAccessor(slot)!;

        accessor.SetChoiceValue("DougCarley Saved", "carley");
        Assert.Equal("carley", accessor.GetChoiceValue("DougCarley Saved"));

        var written = BundleWriter.Write(slot);
        var reloaded = BundleReader.Read(written, "wd1_saveslot1.bundle");

        Assert.Equal("carley", handler.CreateChoiceAccessor(reloaded)!.GetChoiceValue("DougCarley Saved"));
    }

    [Fact]
    public void LiveS1Autosave_ParsesWithoutCrash()
    {
        var path = TestDataHelper.GetPath("S1", "_wd1_saveslot1_autosave.bundle");
        if (!File.Exists(path))
        {
            return;
        }

        var slot = BundleReader.Read(path);
        Assert.NotNull(slot.OuterHeader);
        Assert.NotNull(slot.Metadata);
    }

    [Fact]
    public void LiveS1Save_VersionEntriesMatchExpected()
    {
        var path = TestDataHelper.GetPath("S1", "wd1_saveslot1_live.bundle");
        if (!File.Exists(path))
        {
            return;
        }

        var slot = BundleReader.Read(path);

        Assert.Equal(2, slot.OuterHeader.VersionEntries.Count);
        Assert.Equal(0xE09B099B8076C147UL, slot.OuterHeader.VersionEntries[0].TypeCrc);
        Assert.Equal(0x004F023463D89FB0UL, slot.OuterHeader.VersionEntries[1].TypeCrc);
    }
}
