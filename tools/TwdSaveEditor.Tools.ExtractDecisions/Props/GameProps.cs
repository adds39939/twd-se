using System.Text;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractDecisions.Model;
using TwdSaveEditor.Tools.ExtractDecisions.Scripts;

namespace TwdSaveEditor.Tools.ExtractDecisions.Props;

public sealed partial class GameProps(MetaReader reader)
{
    private const string English = "English";
    private const string EpisodeName = "String Value";
    private const int EpisodeBase = 100;

    public List<LogicKey> ReadLogicKeys(string path)
    {
        var keys = new List<LogicKey>();
        var names = new SymbolLookup();
        foreach (var episode in Load(path).Properties.Select(property => property.Value).OfType<MetaPropertySet>())
        {
            if (episode.Find("Logic Keys") is not MetaPropertySet logic)
                continue;

            var readFrom = Number().Match(Text(episode.Find(EpisodeName))) is { Success: true } match ? int.Parse(match.Value) % EpisodeBase : 0;

            foreach (var property in logic.Properties)
            {
                var name = names.Resolve(property.Key);
                if (property.Value is MetaMap map)
                    keys.Add(new LogicKey(name, true, [.. map.StringEntries.Select(entry => (entry.Key, NodeIds.In(Text(entry.Value)), Text(entry.Value)))], readFrom));
                else if (property.Value is MetaScalar { Text: { } expression })
                    keys.Add(new LogicKey(name, false, [(bool.TrueString.ToLowerInvariant(), NodeIds.In(expression), expression)], readFrom));
            }
        }

        return keys;
    }

    public List<StatChoice> ReadStats(string path)
    {
        var choices = new List<StatChoice>();
        if (Load(path).Find("Choices") is not MetaPropertySet list)
            return choices;

        foreach (var choice in list.Properties.Select(property => property.Value).OfType<MetaPropertySet>())
        {
            var options = (choice.Find("Options") as MetaPropertySet)?.Properties
                .OrderBy(property => SymbolLookup.Index(property.Key))
                .Select(property => property.Value)
                .OfType<MetaPropertySet>()
                .Select(option => (Text(option.Find("Description")), NodeIds.Required(Text(option.Find("expression"))), Text(option.Find("expression"))))
                .ToList() ?? [];

            choices.Add(new StatChoice(
                int.Parse(Text(choice.Find("Episode"))) % 100,
                int.Parse(Text(choice.Find("seq"))),
                Text(choice.Find("Description")),
                Text(choice.Find("Category")),
                options));
        }

        return [.. choices.OrderBy(choice => choice.Episode).ThenBy(choice => choice.Sequence)];
    }

    [GeneratedRegex("\\d+$")]
    private static partial Regex Number();

    private MetaPropertySet Load(string path) =>
        reader.ReadPropertySet(File.ReadAllBytes(path)) ?? throw new InvalidDataException($"{path} is not a property set.");

    private static string Text(MetaNode? node) => node switch
    {
        MetaScalar { Text: { } text } => TextFormat.DecodeUtf8(Encoding.Latin1.GetBytes(text)),
        MetaPropertySet localized => Text(localized.Find(English)),
        _ => string.Empty,
    };
}
