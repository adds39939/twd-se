using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Props;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractAllChoices.Model;

namespace TwdSaveEditor.Tools.ExtractAllChoices.Choices;

public sealed class ChoiceArchiveReader(BlowfishV7 cipher, TextWriter output)
{
    private static readonly string[] ChoiceFiles = ["choice.prop", "cmsWorldChoicesParsingData.prop", "dialog_choices.prop"];

    private static readonly byte[] EnglishTextKey = Bytes.FromU64(PropKeys.EnglishText);

    public List<Choice>? Read(string name, string path)
    {
        output.WriteLine();
        output.WriteLine(new string('=', 60));
        output.WriteLine($"Processing: {name}");

        if (!File.Exists(path))
        {
            output.WriteLine("  ERROR: File not found!");
            return null;
        }

        var data = EcttArchive.Read(path, cipher);
        if (data is not { Length: > 0 })
        {
            output.WriteLine("  ERROR: Failed to decrypt archive");
            return null;
        }

        output.WriteLine($"  Decrypted size: {data.Length} bytes");

        var files = InnerArchive.Parse(data);
        output.WriteLine($"  Files found: {files.Count}");

        var choiceName = ChoiceFiles.FirstOrDefault(files.ContainsKey);
        if (choiceName == null || files[choiceName].IsEmpty)
            choiceName = files.Keys.FirstOrDefault(IsChoiceProp);

        if (choiceName == null || files[choiceName].IsEmpty)
        {
            output.WriteLine("  WARNING: No choice.prop found!");
            return null;
        }

        var choiceData = files[choiceName].Span;
        output.WriteLine($"  Found: {choiceName} ({choiceData.Length} bytes)");

        if (Bytes.IndexOf(choiceData, EnglishTextKey) < 0)
        {
            output.WriteLine($"  NOTE: {choiceName} does not contain English text key");
            output.WriteLine("  This season may store choices in a different format.");
            return null;
        }

        var definition = PropMetaStream.ReadDefinition(choiceData);
        if (definition is not { Length: > 0 })
        {
            output.WriteLine("  ERROR: Failed to parse MetaStream");
            return null;
        }

        output.WriteLine($"  MetaStream definition data: {definition.Length} bytes");

        ParsedPropertySet? propertySet;
        try
        {
            propertySet = PropertySetParser.Parse(new PropReader(definition));
        }
        catch (Exception e)
        {
            output.WriteLine($"  ERROR parsing PropertySet: {e.Message}");
            return null;
        }

        if (propertySet == null)
        {
            output.WriteLine("  ERROR: PropertySet parse returned None");
            return null;
        }

        var choices = new List<Choice>();
        Walk(propertySet, choices, 0);
        output.WriteLine($"  Choices extracted: {choices.Count}");
        return choices;
    }

    private static bool IsChoiceProp(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.Contains("choice") && lower.EndsWith(".prop", StringComparison.Ordinal);
    }

    private static void Walk(ParsedPropertySet propertySet, List<Choice> results, int depth)
    {
        var strings = new OrderedDictionary<ulong, string>();
        var integers = new OrderedDictionary<ulong, int>();
        var nested = new List<ParsedPropertySet>();

        foreach (var group in propertySet.Groups)
        {
            foreach (var property in group.Properties)
            {
                switch (property.Value)
                {
                    case string text when group.Type == PropTypes.String:
                        strings[property.Key] = text;
                        break;
                    case int number when group.Type == PropTypes.Int32:
                        integers[property.Key] = number;
                        break;
                    case ParsedPropertySet child when PropTypes.IsNested(group.Type):
                        nested.Add(child);
                        break;
                }
            }
        }

        var englishText = strings.GetValueOrDefault(PropKeys.EnglishText);
        if (!string.IsNullOrEmpty(englishText))
        {
            string? expressionId = null;
            int? episode = null;
            foreach (var (key, value) in strings)
            {
                if (key is PropKeys.EnglishText or PropKeys.Guid)
                    continue;
                if (value.Length > 5 && value.All(char.IsAsciiDigit))
                    expressionId = value;
            }

            foreach (var value in integers.Values)
            {
                if (value is >= 100 and <= 999)
                    episode = value;
            }

            results.Add(new Choice(TelltaleMarkup.Strip(englishText), strings.GetValueOrDefault(PropKeys.Guid), depth, episode, expressionId));
        }

        foreach (var child in nested)
            Walk(child, results, depth + 1);
    }
}
