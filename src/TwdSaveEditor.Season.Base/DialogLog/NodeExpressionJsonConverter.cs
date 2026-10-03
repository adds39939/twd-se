using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwdSaveEditor.Season.Base.DialogLog;

public sealed class NodeExpressionJsonConverter : JsonConverter<NodeExpression>
{
    public override NodeExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        NodeExpression.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, NodeExpression value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Text);
}
