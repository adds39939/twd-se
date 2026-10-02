namespace TwdSaveEditor.Tools.Common.Inventory;

public sealed record TimelinePoint(string Id, string Script, string? ChapterId, IReadOnlyCollection<string> Setup);
