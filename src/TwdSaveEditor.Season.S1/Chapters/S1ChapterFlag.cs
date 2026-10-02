using System.Text.Json;

namespace TwdSaveEditor.Season.S1.Chapters;

public sealed record S1ChapterFlag(string Agent, string Key, JsonElement Value, string? Scene = null);
