namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

public sealed record MenuEntry(string Group, string Title, string Script, IReadOnlyList<Flag> Flags);
