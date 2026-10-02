using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Tests.Common.Saves;
using TwdSaveEditor.Tests.Common.Data;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Season.Common.Tests.Handlers;

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
    [InlineData("s1")]
    [InlineData("s2")]
    [InlineData("s3")]
    [InlineData("s4")]
    [InlineData("michonne")]
    public void Presets_NameDecisionsAndOptionsTheSeasonHas(string seasonKey)
    {
        var season = Registry.Get(seasonKey)!;
        var provider = Assert.IsAssignableFrom<IChoicePresetProvider>(season);

        Assert.NotEmpty(provider.Presets);
        Assert.Equal(provider.Presets.Count, provider.Presets.Select(preset => preset.Name).Distinct().Count());
        foreach (var selection in provider.Presets.SelectMany(preset => preset.Selections))
        {
            var choice = season.Choices.SingleOrDefault(candidate => candidate.ChoiceKey == selection.ChoiceKey);
            Assert.True(choice != null, $"{seasonKey}: no decision {selection.ChoiceKey}");
            Assert.Contains(choice.Options, option => option.Value == selection.Value);
        }
    }

    [Theory]
    [InlineData("s1", 1, "s1", 1)]
    [InlineData("s1", 5, "s1", 5)]
    [InlineData("s1", 6, "s1_400days", 1)]
    [InlineData("s3", 4, "s3", 4)]
    public void DecisionGroupOf_PointsAtTheCardOfTheEpisode(string seasonKey, int episode, string groupSeason, int groupEpisode)
    {
        Assert.Equal((groupSeason, groupEpisode), Registry.Get(seasonKey)!.DecisionGroupOf(episode));
    }

    [Theory]
    [InlineData("s1", 2)]
    [InlineData("s2", 4)]
    [InlineData("s3", 4)]
    [InlineData("s4", 1)]
    [InlineData("michonne", 0)]
    public void Presets_MarkThoseThatRevealAnEnding(string seasonKey, int revealing)
    {
        var presets = Assert.IsAssignableFrom<IChoicePresetProvider>(Registry.Get(seasonKey)).Presets;

        Assert.Equal(revealing, presets.Count(preset => preset.RevealsEnding));
        Assert.All(presets.Where(preset => preset.RevealsEnding), preset => Assert.Matches("^(Ending|Season 2|Trust AJ)", preset.Name));
    }

    [Fact]
    public void FourHundredDays_HasNoPresets()
    {
        Assert.Empty(Assert.IsAssignableFrom<IChoicePresetProvider>(Registry.Get("s1_400days")).Presets);
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s2")]
    [InlineData("s3")]
    [InlineData("s4")]
    [InlineData("michonne")]
    public void EverySeasonWithSaveFilesBesideTheSlot_ImplementsTheCompanionCapability(string seasonKey)
    {
        Assert.True(Registry.Get(seasonKey) is ICompanionFileHandler);
    }
    [Fact]
    public void DetectedSeasonKey_WorksForAllPrefixes()
    {
        var testCases = new[]
        {
            ("wd1_saveslot1.bundle", "s1"), ("wd2_saveslot1.bundle", "s2"),
            ("wd3_saveslot1.bundle", "s3"), ("wd4_saveslot1.bundle", "s4"),
            ("wdm_saveslot1.bundle", "michonne"),
        };
        foreach (var (fileName, expected) in testCases)
        {
            var detected = Registry.DetectFromFileName(fileName)?.SeasonKey;
            Assert.Equal(expected, detected);
        }
    }

    [Fact]
    public void ChoiceDatabase_HasDefinitionsForAllSeasons()
    {
        var expected = new Dictionary<string, int>
        {
            ["s1"] = 30, ["s1_400days"] = 5, ["s2"] = 25,
            ["michonne"] = 15, ["s3"] = 24, ["s4"] = 15,
        };
        foreach (var (season, minCount) in expected)
        {
            var choices = TestSeasons.ChoicesFor(season).ToList();
            Assert.True(choices.Count >= minCount,
                $"Season {season}: expected >= {minCount}, got {choices.Count}");
        }
    }

    [Fact]
    public void S1ToS2_ChoiceImport_CopiesAllChoices()
    {
        var s1 = Registry.CreateSave("s1", 3, "wd1_test.bundle");
        s1.DetectedSeasonKey = "s1";
        var s1Accessor = Registry.Get("s1")!.CreateChoiceAccessor(s1)!;
        s1Accessor.SetChoiceValue("DougCarley Saved", "doug");

        var s2 = Registry.CreateSave("s2", 1, "wd2_test.bundle");
        var s2Handler = Registry.Get("s2")!;
        ((IChoiceImporter)s2Handler).ImportChoices(s1, s2);

        Assert.Equal("doug", s2Handler.CreateChoiceAccessor(s2)!.GetChoiceValue("DougCarley Saved"));

        var written = BundleWriter.Write(s2);
        var reloaded = BundleReader.Read(written, "wd2_test.bundle");
        Assert.Equal("doug", reloaded.Choices!.GetString("DougCarley Saved"));
        Assert.NotNull(reloaded.Choices.Find("ChoiceTracker - 101"));
    }

}
