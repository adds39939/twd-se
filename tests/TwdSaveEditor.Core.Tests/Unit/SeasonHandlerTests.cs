using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Tests.Integration;
using TwdSaveEditor.Core.Tests.Support;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

public class SeasonHandlerTests
{
    private static readonly ISeasonRegistry Registry = TestSeasons.Registry;

    [Fact]
    public void Seasons_HaveUniqueKeysAndDisplayData()
    {
        var seasons = Registry.All;

        Assert.Equal(seasons.Count, seasons.Select(s => s.SeasonKey).Distinct().Count());
        Assert.All(seasons, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.ShortName));
            Assert.NotEmpty(s.Episodes);
            Assert.NotEmpty(s.Choices);
            Assert.Contains(s.SeasonKey, s.IncludedSeasonKeys);
            Assert.All(s.Choices, c => Assert.Equal(s.SeasonKey, c.SeasonKey));
        });
    }

    [Fact]
    public void Seasons_ReferToRegisteredSeasons()
    {
        foreach (var season in Registry.All)
        {
            Assert.All(season.IncludedSeasonKeys, key => Assert.NotNull(Registry.Get(key)));
            Assert.All(season.ImportsFromSeasonKeys, key => Assert.NotNull(Registry.Get(key)));
        }
    }

    [Theory]
    [InlineData("wd1_saveslot1.bundle", "s1")]
    [InlineData("_wd1_saveslot1_autosave.bundle", "s1")]
    [InlineData("wd2_saveslot1.bundle", "s2")]
    [InlineData("WD3_SaveSlot1.bundle", "s3")]
    [InlineData("wd4_saveslot1.bundle", "s4")]
    [InlineData("wdm_saveslot4.bundle", "michonne")]
    public void DetectFromFileName_FindsSeason(string fileName, string expectedSeasonKey)
    {
        Assert.Equal(expectedSeasonKey, Registry.DetectFromFileName(fileName)?.SeasonKey);
    }

    [Fact]
    public void DetectFromFileName_UnknownPrefix_ReturnsNull()
    {
        Assert.Null(Registry.DetectFromFileName("prefs.bundle"));
    }

    [Fact]
    public void S1Save_IncludesThe400DaysChoices()
    {
        Assert.Equal(["s1", "s1_400days"], Registry.Get("s1")!.IncludedSeasonKeys);
    }

    [Theory]
    [InlineData("s1", "s2")]
    [InlineData("s1_400days", "s2")]
    [InlineData("s3", "s4")]
    public void ImportChain_FollowsTheGame(string sourceSeasonKey, string targetSeasonKey)
    {
        var targets = Registry.All
            .Where(s => s.ImportsFromSeasonKeys.Contains(sourceSeasonKey))
            .Select(s => s.SeasonKey);

        Assert.Equal([targetSeasonKey], targets);
    }

    [Theory]
    [InlineData("s4")]
    [InlineData("michonne")]
    public void ImportChain_EndsAtStandaloneSeasons(string sourceSeasonKey)
    {
        Assert.DoesNotContain(Registry.All, s => s.ImportsFromSeasonKeys.Contains(sourceSeasonKey));
    }

    [Fact]
    public void ChoiceImporter_CopiesChoicesFromEarlierSeason()
    {
        var importer = Assert.IsAssignableFrom<IChoiceImporter>(Registry.Get("s2"));

        var s1 = Registry.CreateSave("s1", 2, "wd1_saveslot1.bundle");
        s1.DetectedSeasonKey = "s1";
        var s2 = Registry.CreateSave("s2", 1, "wd2_saveslot1.bundle");
        s2.DetectedSeasonKey = "s2";

        var s1Accessor = Registry.Get("s1")!.CreateChoiceAccessor(s1)!;
        s1Accessor.SetChoiceValue("DougCarley Saved", "doug");

        Assert.True(importer.CanImportFrom(s1));
        Assert.False(importer.CanImportFrom(s2));

        importer.ImportChoices(s1, s2);

        var s2Accessor = Registry.Get("s2")!.CreateChoiceAccessor(s2)!;
        Assert.Equal("doug", s2Accessor.GetChoiceValue("DougCarley Saved"));
        Assert.Equal("doug", s2.Choices!.GetString("DougCarley Saved"));
    }

    [Fact]
    public void Presets_ApplyThroughTheChoiceAccessor()
    {
        var season = Registry.Get("s4")!;
        var provider = Assert.IsAssignableFrom<IChoicePresetProvider>(season);
        var slot = Registry.CreateSave("s4", 4, "wd4_saveslot1.bundle");
        var accessor = season.CreateChoiceAccessor(slot)!;

        Assert.NotEmpty(provider.Presets);
        foreach (var preset in provider.Presets)
        {
            Assert.NotEmpty(preset.Selections);
            foreach (var selection in preset.Selections)
            {
                var choice = Assert.Single(season.Choices, c => c.ChoiceKey == selection.ChoiceKey);
                Assert.Contains(choice.Options, o => o.Value == selection.Value);

                accessor.SetChoiceValue(selection.ChoiceKey, selection.Value);
                Assert.Equal(selection.Value, accessor.GetChoiceValue(selection.ChoiceKey));
            }
        }
    }

    [Theory]
    [InlineData("s4")]
    public void SeasonsWithoutCompanionFiles_DoNotImplementTheCapability(string seasonKey)
    {
        Assert.False(Registry.Get(seasonKey) is ICompanionFileHandler);
    }

    [Fact]
    public void CompanionFiles_AreFoundInPageOrder()
    {
        var companion = Assert.IsAssignableFrom<ICompanionFileHandler>(Registry.Get("michonne"));

        string[] directory =
        [
            "wdm_saveslot4.bundle",
            "_wdm_saveslot4_id_Page1971.epage",
            "_wdm_saveslot4_id_Page971.epage",
            "_wdm_saveslot4_id.estore",
            "_wdm_saveslot5_id.estore",
            "_wdm_saveslot5_id_Page971.epage",
            "wd1_saveslot1.bundle",
        ];

        var files = companion.FindCompanionFiles("wdm_saveslot4.bundle", directory);

        Assert.Equal(
        [
            "_wdm_saveslot4_id.estore",
            "_wdm_saveslot4_id_Page971.epage",
            "_wdm_saveslot4_id_Page1971.epage",
        ], files);
    }

    [Fact]
    public void CompanionFiles_NotFoundWithoutEStore()
    {
        var companion = Assert.IsAssignableFrom<ICompanionFileHandler>(Registry.Get("michonne"));

        var files = companion.FindCompanionFiles("wdm_saveslot4.bundle",
            ["wdm_saveslot4.bundle", "_wdm_saveslot4_id_Page971.epage"]);

        Assert.Empty(files);
    }

    [Fact]
    public void CompanionFiles_AttachRealEventLog()
    {
        var season = Registry.Get("michonne")!;
        var companion = Assert.IsAssignableFrom<ICompanionFileHandler>(season);
        var slot = LoadRealMichonneSlot(companion);

        Assert.NotNull(slot.LoadedEventLogEntries);
        Assert.NotEmpty(slot.LoadedEventLogEntries);
        Assert.Equal("_wdm_saveslot4_id.estore", slot.EStorePath);
        Assert.Single(slot.EPagePaths!);
        Assert.Equal(
            [slot.EStorePath!, .. slot.EPagePaths!],
            companion.GetCompanionFileNames(slot));

    }

    [Theory]
    [InlineData("michonne", "wdm_saveslot1.bundle")]
    public void CompanionFiles_NewSaveCreatesEventLog(string seasonKey, string fileName)
    {
        var season = Registry.Get(seasonKey)!;
        var companion = Assert.IsAssignableFrom<ICompanionFileHandler>(season);
        var slot = Registry.CreateSave(seasonKey, 1, fileName);

        var files = companion.BuildCompanionFiles(slot);
        Assert.Equal(2, files.Count);
        companion.AttachCompanionFiles(slot, files);

        Assert.Equal(slot.PendingEventLogEntries!.Count, slot.LoadedEventLogEntries!.Count);
        Assert.Equal(files.Select(f => f.Name), companion.GetCompanionFileNames(slot));

        var accessor = season.CreateChoiceAccessor(slot)!;
        var detected = season.Choices.Where(c => c.Episode <= 1).Select(accessor.DetectCurrentChoice).ToList();
        Assert.Contains(0, detected);
        Assert.All(detected, index => Assert.True(index is 0 or -1));
    }

    [Fact]
    public void CompanionFiles_SlotWithoutEventLog_BuildsNothing()
    {
        var companion = Assert.IsAssignableFrom<ICompanionFileHandler>(Registry.Get("michonne"));
        var slot = BundleReader.Read(TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle"));

        Assert.Empty(companion.BuildCompanionFiles(slot));
        Assert.Empty(companion.GetCompanionFileNames(slot));
    }

    private static SaveSlot LoadRealMichonneSlot(ICompanionFileHandler companion)
    {
        var dir = TestDataHelper.GetSeasonDir("Michonne");
        var slot = BundleReader.Read(TestDataHelper.GetPath("Michonne", "wdm_saveslot4.bundle"));

        var directory = Directory.GetFiles(dir).Select(Path.GetFileName).OfType<string>().ToList();
        var files = companion.FindCompanionFiles(slot.FileName, directory)
            .Select(name => new CompanionFile(name, File.ReadAllBytes(Path.Combine(dir, name))))
            .ToList();

        companion.AttachCompanionFiles(slot, files);
        return slot;
    }
}
