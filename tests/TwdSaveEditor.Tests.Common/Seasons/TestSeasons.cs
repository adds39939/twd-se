using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.S4.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S1.Handlers;

namespace TwdSaveEditor.Tests.Common.Seasons;

public static class TestSeasons
{
    public static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(), new S1_400DaysHandler(), new S2Handler(),
        new MichonneHandler(), new S3Handler(), new S4Handler(),
    ]);

    public static IEnumerable<ChoiceDefinition> AllChoices
        => Registry.All.SelectMany(season => season.Choices);

    public static IEnumerable<ChoiceDefinition> ChoicesFor(string seasonKey)
        => Registry.Get(seasonKey)?.Choices ?? [];

    public static IEnumerable<ChoiceDefinition> ChoicesFor(string seasonKey, int episode)
        => ChoicesFor(seasonKey).Where(c => c.Episode == episode);
}
