using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// High-level accessor for reading/writing game choices and metadata
/// from a parsed bundle save structure.
/// </summary>
public sealed class SaveAccessor
{
    private readonly PropertySet _choices;
    private readonly PropertySet? _metadata;

    public SaveAccessor(PropertySet choices, PropertySet? metadata = null)
    {
        _choices = choices;
        _metadata = metadata;
    }

    // ── Choice reading/writing ─────────────────────────────────────────

    /// <summary>
    /// Get the current value of a choice key from a specific episode's data.
    /// Searches all episode properties in the choices PropertySet.
    /// </summary>
    public string? GetChoiceValue(string choiceKey)
    {
        var prefix = choiceKey + " - ";
        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
                continue;

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                    continue;

                var entries = ParseStringBoolArray(raw.Data);
                foreach (var (str, _) in entries)
                {
                    if (str.StartsWith(prefix, StringComparison.Ordinal))
                        return str[prefix.Length..];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Set a choice value. Finds the entry with the matching key and updates it.
    /// If not found, adds it to the first episode property.
    /// </summary>
    public void SetChoiceValue(string choiceKey, string value)
    {
        var prefix = choiceKey + " - ";
        var newEntry = choiceKey + " - " + value;

        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
                continue;

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                    continue;

                var entries = ParseStringBoolArray(raw.Data);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].str.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        entries[i] = (newEntry, entries[i].boolVal);
                        prop.Value = new RawBytesValue(SerializeStringBoolArray(entries), raw.TypeSymbol);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Detect which option index matches the current state for a choice definition.
    /// Returns -1 if the choice key is not found in the save data.
    /// </summary>
    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        var currentValue = GetChoiceValue(choice.ChoiceKey);
        if (currentValue == null)
            return -1;

        for (int i = 0; i < choice.Options.Length; i++)
        {
            if (choice.Options[i].Value.Equals(currentValue, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Apply a choice option, writing its value to the save data.
    /// </summary>
    public void ApplyChoice(ChoiceDefinition choice, int optionIndex)
    {
        var opt = choice.Options[optionIndex];
        SetChoiceValue(choice.ChoiceKey, opt.Value);
    }

    /// <summary>
    /// Get all choice entries (key-value pairs) from all episodes.
    /// </summary>
    public List<(string key, string value)> GetAllChoices()
    {
        var result = new List<(string, string)>();
        foreach (var group in _choices.TypeGroups)
        {
            if (group.TypeSymbol.Value != TelltaleTypes.ChoicesContainer)
                continue;

            foreach (var prop in group.Properties)
            {
                if (prop.Value is not RawBytesValue raw)
                    continue;

                var entries = ParseStringBoolArray(raw.Data);
                foreach (var (str, _) in entries)
                {
                    var sepIdx = str.IndexOf(" - ", StringComparison.Ordinal);
                    if (sepIdx >= 0)
                        result.Add((str[..sepIdx], str[(sepIdx + 3)..]));
                }
            }
        }
        return result;
    }

    // ── Metadata reading ───────────────────────────────────────────────

    /// <summary>
    /// Get a string value from the metadata PropertySet.
    /// </summary>
    public string? GetMetadataString(string keyName)
    {
        if (_metadata == null) return null;
        var symbol = Symbol.FromString(keyName);
        var prop = _metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        return (prop?.Value as StringValue)?.Value;
    }

    /// <summary>
    /// Get an int value from the metadata PropertySet.
    /// </summary>
    public int? GetMetadataInt(string keyName)
    {
        if (_metadata == null) return null;
        var symbol = Symbol.FromString(keyName);
        var prop = _metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        return (prop?.Value as IntValue)?.Value;
    }

    // ── String+Bool array serialization ────────────────────────────────

    /// <summary>
    /// Parse the raw value bytes of the ChoicesContainer type:
    /// u32(count) + count x (u32(strlen) + chars + u8(bool))
    /// </summary>
    public static List<(string str, bool boolVal)> ParseStringBoolArray(byte[] data)
    {
        var result = new List<(string, bool)>();
        var pos = 0;

        // The raw bytes start with u32 count
        if (data.Length < 4) return result;
        var count = BitConverter.ToUInt32(data, pos);
        pos += 4;

        for (uint i = 0; i < count && pos < data.Length; i++)
        {
            var strLen = BitConverter.ToInt32(data, pos);
            pos += 4;
            var str = System.Text.Encoding.ASCII.GetString(data, pos, strLen);
            pos += strLen;
            var boolVal = pos < data.Length && data[pos] == 0x31;
            pos++;
            result.Add((str, boolVal));
        }

        return result;
    }

    /// <summary>
    /// Serialize a list of (string, bool) pairs back to the ChoicesContainer binary format.
    /// </summary>
    public static byte[] SerializeStringBoolArray(List<(string str, bool boolVal)> entries)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((uint)entries.Count);
        foreach (var (str, boolVal) in entries)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(str);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Write((byte)(boolVal ? 0x31 : 0x30));
        }

        return ms.ToArray();
    }
}
