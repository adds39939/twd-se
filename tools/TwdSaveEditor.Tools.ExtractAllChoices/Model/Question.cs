namespace TwdSaveEditor.Tools.ExtractAllChoices.Model;

public sealed class Question(string text, string? guid, int? episode, string? expressionId)
{
    public string Text { get; set; } = text;
    public string? Guid { get; } = guid;
    public int? Episode { get; set; } = episode;
    public string? ExpressionId { get; } = expressionId;
    public List<Option> Options { get; set; } = [];
}
