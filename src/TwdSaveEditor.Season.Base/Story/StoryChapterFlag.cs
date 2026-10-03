using System.Text.Json;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed record StoryChapterFlag(string Key, JsonElement Value)
{
    public bool IsSetIn(PropertySet? properties) => Value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => properties?.GetBool(Key) == Value.GetBoolean(),
        JsonValueKind.Number => properties?.GetInt(Key) == Value.GetInt32(),
        JsonValueKind.String => properties?.GetString(Key) == Value.GetString(),
        _ => properties?.Find(Key) != null,
    };
}
