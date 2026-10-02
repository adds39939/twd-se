using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason2Choices.Model;
using TwdSaveEditor.Tools.ExtractSeason2Choices.Scripts;

namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Props;

public sealed class GameProps(MetaReader reader)
{
    private const string English = "English";

    public List<LogicKey> ReadLogicKeys(string path)
    {
        var keys = new List<LogicKey>();
        var names = new SymbolLookup();
        foreach (var episode in Load(path).Properties.Select(property => property.Value).OfType<MetaPropertySet>())
        {
            if (episode.Find("Logic Keys") is not MetaPropertySet logic)
                continue;

            foreach (var property in logic.Properties)
            {
                var name = names.Resolve(property.Key);
                if (property.Value is MetaMap map)
                    keys.Add(new LogicKey(name, true, [.. map.StringEntries.Select(entry => (entry.Key, NodeIds.In((entry.Value as MetaScalar)?.Text ?? string.Empty)))]));
                else if (property.Value is MetaScalar { Text: { } expression })
                    keys.Add(new LogicKey(name, false, [(bool.TrueString.ToLowerInvariant(), NodeIds.In(expression))]));
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
                .Select(option => (Text(option.Find("Description")), NodeIds.Required(Text(option.Find("expression")))))
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

    private MetaPropertySet Load(string path) =>
        reader.ReadPropertySet(File.ReadAllBytes(path)) ?? throw new InvalidDataException($"{path} is not a property set.");

    private static string Text(MetaNode? node) => node switch
    {
        MetaScalar { Text: { } text } => text,
        MetaPropertySet localized => Text(localized.Find(English)),
        _ => string.Empty,
    };
}
