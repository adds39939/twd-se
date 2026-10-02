namespace TwdSaveEditor.Season.S2.Decisions;

public sealed record S2Decision(string ChoiceKey, string? LogicKey, bool LogicIsText, IReadOnlyList<string> Requires, IReadOnlyList<S2DecisionOption> Options)
{
    private const string EpisodePrefix = "Episode 20";

    public int Episode => ChoiceKey[EpisodePrefix.Length] - '0';

    public S2DecisionOption? Find(string value) =>
        Options.FirstOrDefault(option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}
