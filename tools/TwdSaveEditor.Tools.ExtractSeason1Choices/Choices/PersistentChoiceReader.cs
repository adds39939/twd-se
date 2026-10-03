using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason1Choices.Model;

namespace TwdSaveEditor.Tools.ExtractSeason1Choices.Choices;

public sealed class PersistentChoiceReader(BlowfishV7 cipher, MetaReader reader)
{
    public const string Archive = "WDC_pc_ProjectSeason1_data.ttarch2";

    private const int FirstEpisode = 101;
    private const int LastEpisode = 106;

    public List<PersistentChoice> Read(string archivePath)
    {
        var files = GameArchive.ReadFiles(archivePath, cipher);
        var keyNames = Load(files, "persistent.prop");
        var values = Load(files, "statsInfo_values.prop");
        var texts = Load(files, "statsInfo_text.prop");

        var choices = new List<PersistentChoice>();
        for (var episode = FirstEpisode; episode <= LastEpisode; episode++)
        {
            if (keyNames.Find($"Persistent - {episode} - Key Names") is not MetaList keys)
            {
                continue;
            }

            foreach (var key in keys.Strings)
            {
                var text = texts.Find(key) as MetaMap;
                var options = (values.Find(key) as MetaMap)?.StringEntries
                    .Select(entry => new PersistentOption(entry.Key.ToLowerInvariant(), Label(entry.Key, (entry.Value as MetaScalar)?.Text, text)))
                    .ToList() ?? [];

                choices.Add(new PersistentChoice(episode, key, text?.FindText("Description"), options));
            }
        }

        return choices;
    }

    private MetaPropertySet Load(OrderedDictionary<string, ReadOnlyMemory<byte>> files, string name) =>
        reader.ReadPropertySet(files[name].Span) ?? throw new InvalidDataException($"{name} is not a property set");

    private static string Label(string value, string? choiceNumber, MetaMap? text)
    {
        var choiceText = text?.FindText($"Choice {choiceNumber} Text");
        if (string.IsNullOrEmpty(choiceText))
        {
            return value;
        }

        var sentence = char.ToUpperInvariant(choiceText[0]) + choiceText[1..].TrimEnd('.');
        return value is "true" or "false" ? sentence : $"{sentence} ({value})";
    }
}
