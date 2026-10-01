using TwdSaveEditor.Season.Base.Resources;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S1.Persistence;

public static class S1ChoiceCatalog
{
    public const string MainSeasonKey = "s1";
    public const string ExtraEpisodeSeasonKey = "s1_400days";

    private const int EpisodeBase = 100;

    private static readonly Lazy<IReadOnlyList<ChoiceDefinition>> Choices = new(() =>
    [
        .. EmbeddedSeasonData.LoadChoices(typeof(S1ChoiceCatalog).Assembly, MainSeasonKey),
        .. EmbeddedSeasonData.LoadChoices(typeof(S1ChoiceCatalog).Assembly, ExtraEpisodeSeasonKey),
    ]);

    private static readonly Lazy<Dictionary<string, int>> EpisodesByKey = new(() =>
        Choices.Value.ToDictionary(choice => choice.ChoiceKey, PersistentEpisode, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<ChoiceDefinition> All => Choices.Value;

    public static int PersistentEpisode(ChoiceDefinition choice) =>
        choice.SeasonKey == ExtraEpisodeSeasonKey ? PersistentKeys.LastEpisode : EpisodeBase + choice.Episode;

    public static int? PersistentEpisode(string choiceKey) =>
        EpisodesByKey.Value.TryGetValue(choiceKey, out var episode) ? episode : null;

    public static IEnumerable<ChoiceDefinition> Before(int persistentEpisode) =>
        All.Where(choice => PersistentEpisode(choice) < persistentEpisode);
}
