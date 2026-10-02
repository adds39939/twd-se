namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryDecisionData(IReadOnlyList<StoryDecision> Decisions, IReadOnlyList<StoryLogicKey> LogicKeys);
