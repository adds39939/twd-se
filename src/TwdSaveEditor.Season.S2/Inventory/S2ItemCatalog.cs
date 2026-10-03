using TwdSaveEditor.Season.Base.Resources;

namespace TwdSaveEditor.Season.S2.Inventory;

public static class S2ItemCatalog
{
    private const string ResourceSuffix = "s2.items.json";

    private static readonly Lazy<S2ItemData> Data = new(Load);

    public static IReadOnlyList<S2EpisodeItems> All => Data.Value.Episodes;

    public static S2EpisodeItems? ForEpisode(int episode) => All.FirstOrDefault(entry => entry.Episode == episode);

    private static S2ItemData Load()
    {
        return EmbeddedSeasonData.Load<S2ItemData>(typeof(S2ItemCatalog).Assembly, ResourceSuffix) ?? new S2ItemData([]);
    }
}
