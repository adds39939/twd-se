using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.ExtractNodeMappings.EventLog;
using TwdSaveEditor.Tools.ExtractNodeMappings.Expressions;
using TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;
using TwdSaveEditor.Tools.ExtractNodeMappings.Model;

namespace TwdSaveEditor.Tools.ExtractNodeMappings.Reporting;

public static class MappingJson
{
    public static JsonObject Build(IReadOnlyDictionary<string, List<ChoiceMapping>> seasonMappings, EpageHashes epageHashes)
    {
        var json = new JsonObject();
        foreach (var season in Seasons.OutputOrder)
        {
            var mappings = seasonMappings.GetValueOrDefault(season, []);
            if (mappings.Count == 0)
            {
                continue;
            }

            var entries = new JsonArray();
            foreach (var mapping in mappings)
            {
                var options = new JsonArray();
                foreach (var option in mapping.Options)
                {
                    options.Add((JsonNode)BuildOption(option, season == Seasons.Season3, epageHashes));
                }

                entries.Add((JsonNode)new JsonObject
                {
                    ["question"] = mapping.QuestionClean.Length > 0 ? mapping.QuestionClean : mapping.Question,
                    ["episode_code"] = mapping.EpisodeCode,
                    ["target_episode"] = mapping.TargetEpisode,
                    ["options"] = options,
                });
            }

            json[season] = entries;
        }

        return json;
    }

    private static JsonObject BuildOption(OptionMapping option, bool verify, EpageHashes epageHashes)
    {
        var expression = ExpressionParser.Parse(option.Expression);
        var secondary = ExpressionParser.Parse(option.SecondaryExpression);
        var entry = new JsonObject
        {
            ["text"] = option.TextClean.Length > 0 ? option.TextClean : option.Text,
            ["expression_raw"] = option.Expression,
            ["expression_type"] = expression.Type,
            ["secondary_raw"] = option.SecondaryExpression,
            ["guid"] = option.Guid,
        };

        if (expression.IsDecimal)
        {
            entry["crc64_hex"] = expression.HexHash;
            entry["crc64_decimal"] = JsonNode.Parse(expression.Decimal.ToString()!);
            if (verify)
            {
                entry["verified_in_epage"] = epageHashes.HasNode(expression.Decimal);
            }
        }

        if (secondary.IsDecimal)
        {
            entry["secondary_crc64_hex"] = secondary.HexHash;
            entry["secondary_crc64_decimal"] = JsonNode.Parse(secondary.Decimal.ToString()!);
        }

        return entry;
    }
}
