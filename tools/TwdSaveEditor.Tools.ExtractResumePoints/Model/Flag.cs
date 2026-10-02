using System.Text.Json.Nodes;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Model;

public sealed record Flag(string Key, JsonNode Value);
