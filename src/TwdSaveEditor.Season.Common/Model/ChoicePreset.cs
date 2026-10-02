namespace TwdSaveEditor.Season.Common.Model;

public sealed record ChoicePreset(string Name, IReadOnlyList<ChoiceSelection> Selections, bool RevealsEnding = false);
