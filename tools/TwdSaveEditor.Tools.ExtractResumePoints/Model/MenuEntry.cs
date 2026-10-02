namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record MenuEntry(string Group, string Title, string Script, IReadOnlyList<Flag> Flags);
