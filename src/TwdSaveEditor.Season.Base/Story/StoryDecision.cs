namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryDecision(string ChoiceKey, int Episode, IReadOnlyList<StoryDecisionOption> Options)
{
    public StoryDecisionOption? Find(string value) =>
        Options.FirstOrDefault(option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}
