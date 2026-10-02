namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record Chapter(string Handler, string Title, string Group, string? Script, IReadOnlyList<Assignment> Assignments, bool Conditional);
