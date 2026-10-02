namespace TwdSaveEditor.Season.S3.Decisions;

public sealed record S3Decision(string ChoiceKey, int Episode, IReadOnlyList<S3DecisionOption> Options)
{
    public S3DecisionOption? Find(string value) =>
        Options.FirstOrDefault(option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}
