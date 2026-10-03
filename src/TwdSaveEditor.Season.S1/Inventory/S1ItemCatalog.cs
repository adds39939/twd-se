using TwdSaveEditor.Season.Base.Resources;

namespace TwdSaveEditor.Season.S1.Inventory;

public static class S1ItemCatalog
{
    private const string ResourceSuffix = "s1.items.json";

    private static readonly Lazy<S1ItemData> Data = new(Load);

    public static IReadOnlyList<S1EpisodeItems> All => Data.Value.Episodes;

    public static S1EpisodeItems? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S1ItemData Load()
    {
        return EmbeddedSeasonData.Load<S1ItemData>(typeof(S1ItemCatalog).Assembly, ResourceSuffix) ?? new S1ItemData([]);
    }
}
