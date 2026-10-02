namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryLogicKey(string Key, int ReadFrom, bool Text, IReadOnlyList<StoryLogicValue> Values);
