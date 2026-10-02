using System.Text.Json;

namespace TwdSaveEditor.Season.S3.Chapters;

public sealed record S3ChapterFlag(string Key, JsonElement Value);
