using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Accessors;

public sealed class SaveAccessor : IChoiceAccessor
{
    private readonly PropertySet? _choices;
    private readonly PropertySet? _metadata;

    public SaveAccessor(PropertySet? choices, PropertySet? metadata = null)
    {
        _choices = choices;
        _metadata = metadata;
    }

    public bool HasChoices => _choices != null;

    public string? GetChoiceValue(string choiceKey)
    {
        if (_choices == null)
        {
            return null;
        }

        var prefix = choiceKey + " - ";
        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
            {
                continue;
            }

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                {
                    continue;
                }

                var entries = ChoicesContainer.Parse(raw.Data);
                foreach (var (str, _) in entries)
                {
                    if (str.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return str[prefix.Length..];
                    }
                }
            }
        }
        return null;
    }

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (_choices == null)
        {
            throw new InvalidOperationException("Cannot set choice value: no choices PropertySet loaded.");
        }

        var prefix = choiceKey + " - ";
        var newEntry = choiceKey + " - " + value;

        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
            {
                continue;
            }

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                {
                    continue;
                }

                var entries = ChoicesContainer.Parse(raw.Data);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].str.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        entries[i] = (newEntry, entries[i].boolVal);
                        prop.Value = new RawBytesValue(ChoicesContainer.Serialize(entries), raw.TypeSymbol);
                        return;
                    }
                }

                entries.Add((newEntry, true));
                prop.Value = new RawBytesValue(ChoicesContainer.Serialize(entries), raw.TypeSymbol);
                return;
            }
        }

        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var newGroup = new TypeGroup(typeSymbol);
        var newEntries = new List<(string, bool)> { (newEntry, true) };
        newGroup.Properties.Add(new Property(
            Symbol.FromString("choices"),
            new RawBytesValue(ChoicesContainer.Serialize(newEntries), typeSymbol)));
        _choices.TypeGroups.Add(newGroup);
    }

    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        var currentValue = GetChoiceValue(choice.ChoiceKey);
        if (currentValue == null)
        {
            return -1;
        }

        for (int i = 0; i < choice.Options.Length; i++)
        {
            if (choice.Options[i].Value.Equals(currentValue, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex)
    {
        var opt = choice.Options[optionIndex];
        SetChoiceValue(choice.ChoiceKey, opt.Value);
    }

    public List<(string key, string value)> GetAllChoices()
    {
        var result = new List<(string, string)>();
        if (_choices == null)
        {
            return result;
        }

        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
            {
                continue;
            }

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                {
                    continue;
                }

                var entries = ChoicesContainer.Parse(raw.Data);
                foreach (var (str, _) in entries)
                {
                    var sepIdx = str.IndexOf(" - ", StringComparison.Ordinal);
                    if (sepIdx >= 0)
                    {
                        result.Add((str[..sepIdx], str[(sepIdx + 3)..]));
                    }
                }
            }
        }
        return result;
    }

    public string? GetMetadataString(string keyName)
    {
        if (_metadata == null)
        {
            return null;
        }

        var symbol = Symbol.FromString(keyName);
        var prop = _metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        return (prop?.Value as StringValue)?.Value;
    }

    public int? GetMetadataInt(string keyName)
    {
        if (_metadata == null)
        {
            return null;
        }

        var symbol = Symbol.FromString(keyName);
        var prop = _metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        return (prop?.Value as IntValue)?.Value;
    }
}
