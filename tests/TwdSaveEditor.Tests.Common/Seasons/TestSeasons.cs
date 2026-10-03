using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Bootstrap.Extensions;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Tests.Common.Seasons;

public static class TestSeasons
{
    public static readonly IServiceProvider Services = new ServiceCollection().AddTwdSaveEditorServices().BuildServiceProvider();

    public static readonly ISeasonRegistry Registry = Services.GetRequiredService<ISeasonRegistry>();

    public static T Resolve<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public static T Handler<T>(string seasonKey)
        where T : class, ISeasonHandler
        => Registry.Get(seasonKey) as T ?? throw new InvalidOperationException($"No {typeof(T).Name} is registered for {seasonKey}.");

    public static IEnumerable<ChoiceDefinition> AllChoices
        => Registry.All.SelectMany(season => season.Choices);

    public static IEnumerable<ChoiceDefinition> ChoicesFor(string seasonKey)
        => Registry.Get(seasonKey)?.Choices ?? [];

    public static IEnumerable<ChoiceDefinition> ChoicesFor(string seasonKey, int episode)
        => ChoicesFor(seasonKey).Where(c => c.Episode == episode);
}
