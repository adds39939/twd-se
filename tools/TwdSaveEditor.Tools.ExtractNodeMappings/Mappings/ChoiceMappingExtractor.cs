using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Json;
using TwdSaveEditor.Tools.Common.Props;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractNodeMappings.Model;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;

public static class ChoiceMappingExtractor
{
    private const string KeyLocalizedText = "0x662E2F86618F19DA";
    private const string KeyEnglishText = "0x51B8A88CAD6DA2B1";
    private const string KeyExpression = "0x704F239B5CCB1A02";
    private const string KeySecondaryExpression = "0xD9960DF963F20638";
    private const string KeyEpisodeCode = "0x35DF01C0CB402E67";
    private const string KeyTargetEpisode = "0xF355012A92A676CE";
    private const string KeyOptions = "0x8B92202445FF971A";
    private const string KeyGuid = "0xA955F65339C75510";
    private const string KeyPercentage = "0x7C7B27DCD663E32E";

    public static List<ChoiceMapping> FromJsonFile(string path) =>
        FromJson(JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? []);

    public static List<ChoiceMapping> FromBinaryProp(ReadOnlySpan<byte> propData, string season, bool saveJson, TextWriter output)
    {
        var definition = PropMetaStream.ReadDefinition(propData);
        if (definition is not { Length: > 0 })
        {
            output.WriteLine($"  ERROR: Failed to parse MetaStream for {season}");
            return [];
        }

        output.WriteLine($"  MetaStream definition data: {definition.Length} bytes");

        ParsedPropertySet? propertySet;
        try
        {
            propertySet = PropertySetParser.Parse(new PropReader(definition));
        }
        catch (Exception e)
        {
            output.WriteLine($"  ERROR parsing PropertySet for {season}: {e.Message}");
            return [];
        }

        if (propertySet == null)
        {
            output.WriteLine($"  ERROR: PropertySet parse returned None for {season}");
            return [];
        }

        var json = ToJson(propertySet);
        if (saveJson)
            SaveJson(json, season, output);

        return FromJson(json);
    }

    private static void SaveJson(JsonObject json, string season, TextWriter output)
    {
        var path = Path.Combine(ToolPaths.ToolsDirectory, $"{season.ToLowerInvariant().Replace(' ', '_')}_choice_prop.json");
        try
        {
            File.WriteAllText(path, JsonText.Serialize(json, escapeNonAscii: false));
            output.WriteLine($"  Saved parsed JSON to: {path}");
        }
        catch (IOException e)
        {
            output.WriteLine($"  Warning: could not save JSON: {e.Message}");
        }
    }

    private static JsonObject ToJson(ParsedPropertySet propertySet)
    {
        var result = new JsonObject();
        foreach (var group in propertySet.Groups)
        {
            foreach (var property in group.Properties)
            {
                result[$"0x{property.Key:X16}"] = property.Value switch
                {
                    ParsedPropertySet nested => ToJson(nested),
                    string text => JsonValue.Create(text),
                    bool flag => JsonValue.Create(flag),
                    int number => JsonValue.Create(number),
                    _ => null,
                };
            }
        }

        return result;
    }

    private static List<ChoiceMapping> FromJson(JsonObject data)
    {
        var mappings = new List<ChoiceMapping>();

        var container = data.Select(pair => pair.Value).OfType<JsonObject>().FirstOrDefault(value => value.Count > 2);
        if (container == null)
            return mappings;

        foreach (var (_, choiceNode) in container)
        {
            if (choiceNode is not JsonObject choice)
                continue;

            var question = EnglishText(choice);
            mappings.Add(new ChoiceMapping(
                question,
                TelltaleMarkup.Strip(question),
                Text(choice, KeyExpression),
                Text(choice, KeySecondaryExpression),
                Text(choice, KeyEpisodeCode),
                Text(choice, KeyTargetEpisode),
                ReadOptions(choice)));
        }

        return mappings;
    }

    private static List<OptionMapping> ReadOptions(JsonObject choice)
    {
        var options = new List<OptionMapping>();
        if (choice[KeyOptions] is not JsonObject container)
            return options;

        foreach (var (_, optionNode) in container)
        {
            if (optionNode is not JsonObject option)
                continue;

            var text = EnglishText(option);
            var expression = Text(option, KeyExpression);
            var guid = Text(option, KeyGuid);
            if (expression.Length == 0 && guid.Length > 0 && !guid.StartsWith('{'))
                expression = "{" + guid + "}";

            options.Add(new OptionMapping(
                text,
                TelltaleMarkup.Strip(text),
                expression,
                Text(option, KeySecondaryExpression),
                guid,
                Text(option, KeyPercentage)));
        }

        return options;
    }

    private static string EnglishText(JsonObject node) =>
        node[KeyLocalizedText] is JsonObject texts ? Text(texts, KeyEnglishText) : "";

    private static string Text(JsonObject node, string key) =>
        node[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : "";
}
