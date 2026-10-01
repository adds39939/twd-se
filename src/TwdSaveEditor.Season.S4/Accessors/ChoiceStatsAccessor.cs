using System.Text.RegularExpressions;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S4.Accessors;

public sealed partial class ChoiceStatsAccessor : IChoiceAccessor
{
    private readonly SaveSlot _slot;
    private readonly HashSet<string> _activeGuids;

    private static readonly Dictionary<string, (string ChoiceKey, string OptionValue)> GuidToChoice =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["D5CAC505-D44C-4338-A465-EFA30DF08A5E"] = ("happy_couple", "window"),
            ["48C0A99E-C18D-45D7-B5CC-7798ABBE8296"] = ("aj_bed", "on"),
            ["E3BBFE95-0925-46CA-A64D-C83003084DD0"] = ("aj_bed", "under"),
            ["170B10E1-289B-48FC-8B2E-9D775CFEBF2F"] = ("happy_couple", "killed"),
            ["D4DFE2E9-4A8B-4DA7-B05A-D1C3495D639F"] = ("surrendered_food_abel", "false"),
            ["EB2EFF11-EA62-49B5-81AB-FF4C7742C213"] = ("surrendered_food_abel", "true"),
            ["80E48E39-2D80-406A-B313-008509B46C30"] = ("turned_to_for_help", "violet"),
            ["141CD479-1A00-4F12-A07E-F97FA5E11E77"] = ("turned_to_for_help", "louis"),
            ["59287F15-5C7C-4C87-A3AA-792C16BB5603"] = ("fishing_or_hunting", "fishing"),
            ["1882BD84-079A-4BFD-A984-A64AB71FA581"] = ("follow_violet_louis", "violet"),
            ["D2106F2D-CA58-4F5B-AD6C-8D7CE017C048"] = ("follow_violet_louis", "louis"),
            ["F28A8FAC-A4EF-4E4C-B510-D947FAEF804E"] = ("violet_run_shoot", "shoot"),
            ["7BBED350-29A6-4BD3-B598-C48D39F3BED7"] = ("violet_run_shoot", "run"),
            ["126EB207-831C-4BFF-9D85-FB9D66493BB5"] = ("violetlouis_saved", "violet"),
            ["64ACF62A-FBD5-4513-BA3B-0EBF08C99EF5"] = ("violetlouis_saved", "louis"),
            ["D670AC7F-AF66-4025-B6B7-47F57E4A90F1"] = ("ajs_gun", "kept"),
            ["CBAC299B-41B0-4561-9D9B-3F0900EB8E97"] = ("ajs_gun", "gave"),
            ["29E26E3D-A351-4856-B6EE-FD41F41E7D58"] = ("killed_james_walkers", "killed"),
            ["8075C794-535B-4A7C-B5F5-C5BF056B9466"] = ("killed_james_walkers", "distract"),
            ["AAC14C7F-61E0-4531-B255-DC17F9FCE173"] = ("killed_james_walkers", "spared"),
            ["C9E688C6-D2CF-47C6-929C-F1F852BE939E"] = ("mercy_killed_abel", "true"),
            ["5818A5A9-01C8-483C-9F06-365626F4E7E5"] = ("mercy_killed_abel", "false"),
            ["AB059164-77F9-4FB4-8996-0BA4F96DC361"] = ("aj_attack_dorian", "allowed"),
            ["5A3A8F4F-62BE-40C0-9F31-F39EC2F275DC"] = ("aj_attack_dorian", "stopped"),
            ["44306977-619A-4B50-BCDB-52346B0E18B5"] = ("killed_lilly", "true"),
            ["F84FDD53-A057-48A3-93F6-4A9FACEC8250"] = ("killed_lilly", "false"),
            ["5E15E8C7-7E06-40B6-93CF-7668DB67708D"] = ("forest_walker_kills", "all"),
            ["14F7FB2F-9116-4D47-9624-E7EEF9FF721A"] = ("forest_walker_kills", "none"),
            ["2280AECF-E4EB-4511-85C9-E6504BC7AAD6"] = ("trusted_aj", "true"),
            ["825F90A0-40A1-40FC-A3C6-DD0907FA5F20"] = ("trusted_aj", "false"),
            ["035DB8B1-43B7-4F70-B841-30C7A469A0FF"] = ("bomb_name", "mitch"),
            ["27E86310-2E89-4DC4-AB25-2F5BE9FAF5C7"] = ("bomb_name", "willy"),
        };

    private static readonly Dictionary<(string ChoiceKey, string OptionValue), string> ChoiceToGuid;

    private static readonly Dictionary<string, List<string>> ChoiceKeyToGuids;

    static ChoiceStatsAccessor()
    {
        ChoiceToGuid = [];
        ChoiceKeyToGuids = new(StringComparer.OrdinalIgnoreCase);

        foreach (var (guid, (choiceKey, optionValue)) in GuidToChoice)
        {
            ChoiceToGuid[(choiceKey, optionValue)] = guid;

            if (!ChoiceKeyToGuids.TryGetValue(choiceKey, out var list))
            {
                list = [];
                ChoiceKeyToGuids[choiceKey] = list;
            }
            list.Add(guid);
        }
    }

    public ChoiceStatsAccessor(SaveSlot slot)
    {
        _slot = slot;
        _activeGuids = ParseGuids(GetRawString());
    }

    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        for (int i = 0; i < choice.Options.Length; i++)
        {
            var key = (choice.ChoiceKey, choice.Options[i].Value);
            if (ChoiceToGuid.TryGetValue(key, out var guid) &&
                _activeGuids.Contains(guid.ToUpperInvariant()))
            {
                return i;
            }
        }
        return -1;
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex)
    {
        SetChoiceValue(choice.ChoiceKey, choice.Options[optionIndex].Value);
    }

    public string? GetChoiceValue(string choiceKey)
    {
        if (!ChoiceKeyToGuids.TryGetValue(choiceKey, out var guids))
            return null;

        foreach (var guid in guids)
        {
            if (_activeGuids.Contains(guid.ToUpperInvariant()))
            {
                return GuidToChoice[guid].OptionValue;
            }
        }
        return null;
    }

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (ChoiceKeyToGuids.TryGetValue(choiceKey, out var existingGuids))
        {
            foreach (var guid in existingGuids)
                _activeGuids.Remove(guid.ToUpperInvariant());
        }

        var key = (choiceKey, value);
        if (ChoiceToGuid.TryGetValue(key, out var newGuid))
        {
            _activeGuids.Add(newGuid.ToUpperInvariant());
        }

        WriteGuids();
    }

    private string GetRawString()
    {
        if (_slot.ChoiceStats == null)
            return "";

        var prop = _slot.ChoiceStats.AllProperties.FirstOrDefault();
        if (prop?.Value is StringValue sv)
            return sv.Value;

        return "";
    }

    private void SetRawString(string value)
    {
        if (_slot.ChoiceStats == null)
            return;

        var prop = _slot.ChoiceStats.AllProperties.FirstOrDefault();
        if (prop?.Value is StringValue sv)
            sv.Value = value;
    }

    private static HashSet<string> ParseGuids(string raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(raw))
            return set;

        foreach (Match match in GuidRegex().Matches(raw))
        {
            set.Add(match.Groups[1].Value.ToUpperInvariant());
        }
        return set;
    }

    private void WriteGuids()
    {
        var entries = _activeGuids
            .Select(g => $"( {{{g}}} )")
            .ToArray();
        SetRawString(string.Join("\t", entries));
    }

    [GeneratedRegex(@"\{\s*([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\s*\}")]
    private static partial Regex GuidRegex();
}
