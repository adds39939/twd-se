using System.Text.Json;

namespace TwdSaveEditor.Season.S3.Decisions;

public static class S3DecisionCatalog
{
    private const string ResourceSuffix = "s3.decisions.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<S3DecisionData> Data = new(Load);

    public static IReadOnlyList<S3Decision> All => Data.Value.Decisions;

    public static IReadOnlyList<S3LogicKey> LogicKeys => Data.Value.LogicKeys;

    public static S3Decision? Find(string choiceKey) =>
        All.FirstOrDefault(decision => decision.ChoiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));

    public static S3LogicKey? FindLogicKey(string key) => LogicKeys.FirstOrDefault(logic => logic.Key == key);

    private static S3DecisionData Load()
    {
        var assembly = typeof(S3DecisionCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<S3DecisionData>(stream, JsonOptions) ?? new S3DecisionData([], []);
    }
}
