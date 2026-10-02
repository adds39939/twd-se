using System.Text.Json.Nodes;

namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record Assignment(string Agent, string Key, JsonNode? Value, string? Unresolved, string? Scene = null);
