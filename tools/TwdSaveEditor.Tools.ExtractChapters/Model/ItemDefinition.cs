namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record ItemDefinition(string Agent, string Key, string Name, bool Flag, int MaxCount, string? ChoiceKey = null);
