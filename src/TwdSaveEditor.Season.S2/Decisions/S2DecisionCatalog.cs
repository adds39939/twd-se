using System.Text.Json;

namespace TwdSaveEditor.Season.S2.Decisions;

public static class S2DecisionCatalog
{
    private const string ResourceSuffix = "s2.nodes.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<S2Decision>> Decisions = new(Load);

    public static IReadOnlyList<S2Decision> All => Decisions.Value;

    public static S2Decision? Find(string choiceKey) =>
        All.FirstOrDefault(decision => decision.ChoiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<S2Decision> Load()
    {
        var assembly = typeof(S2DecisionCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<List<S2Decision>>(stream, JsonOptions) ?? [];
    }
}
