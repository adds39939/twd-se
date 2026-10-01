namespace TwdSaveEditor.Tools.ExtractAllChoices.Model;

public sealed record Choice(string Text, string? Guid, int Depth, int? Episode, string? ExpressionId);
