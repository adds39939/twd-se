using TwdSaveEditor.Season.Base.Resources;

namespace TwdSaveEditor.Season.S2.Decisions;

public static class S2DecisionCatalog
{
    private const string ResourceSuffix = "s2.nodes.json";

    private static readonly Lazy<IReadOnlyList<S2Decision>> Decisions = new(Load);

    public static IReadOnlyList<S2Decision> All => Decisions.Value;

    public static S2Decision? Find(string choiceKey) =>
        All.FirstOrDefault(decision => decision.ChoiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<S2Decision> Load()
    {
        return EmbeddedSeasonData.Load<List<S2Decision>>(typeof(S2DecisionCatalog).Assembly, ResourceSuffix) ?? [];
    }
}
