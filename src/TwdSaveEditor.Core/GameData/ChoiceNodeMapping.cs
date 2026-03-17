namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Maps dialog node CRC64 hashes (from EventLog "Executing Dialog Node" records)
/// to choice definitions. Used by S3 and S4 (CRC64 hashes) and Michonne (GUIDs).
/// Hash data extracted from the game's choice.prop expression fields.
/// </summary>
public static class ChoiceNodeMapping
{
    /// <summary>
    /// S3 node hash -> (choiceKey, optionValue).
    /// Hashes appear at bytes 29-36 of the 42-byte EventLog record.
    /// </summary>
    public static readonly Dictionary<ulong, (string ChoiceKey, string OptionValue)> S3Nodes = new()
    {
        // shot_conrad
        [0x2D4BB68B3A6B79B7] = ("shot_conrad", "true"),
        [0x95124598AFDC509B] = ("shot_conrad", "false"),

        // promised_kate
        [0xF650515EC9AA8356] = ("promised_kate", "false"),
        [0x05F6BEEEE2651B6C] = ("promised_kate", "true"),

        // david_about_kate
        [0x9167834B06FEF0A7] = ("david_about_kate", "denied"),
        [0x20D31F475BBE5406] = ("david_about_kate", "nothing"),
        [0xBAA8F6CB39B2F715] = ("david_about_kate", "confessed"),
        [0x800E166EDF2C692D] = ("david_about_kate", "clean"),

        // shot_joan
        [0x80E6B5D0C042A4DB] = ("shot_joan", "true"),
        [0x30AC5DBC402A7EE4] = ("shot_joan", "false"),

        // honored_brother
        [0x1325DF039DD5B4B6] = ("honored_brother", "true"),
        [0xA22AB5978DEC833A] = ("honored_brother", "false"),

        // lingard_fate
        [0x03C8E5D11A82B92B] = ("lingard_fate", "clem"),
        [0x71EB7824BE404986] = ("lingard_fate", "assisted"),
        [0x4D8ABA46AFB0AF47] = ("lingard_fate", "refused"),

        // trusted_jesus
        [0x754A9E878DE95CB8] = ("trusted_jesus", "true"),
        [0xB5F43A2F700B402D] = ("trusted_jesus", "false"),

        // fought_david
        [0xE7BEE76E5F420822] = ("fought_david", "true"),
        [0x0F02D1B8938DAA32] = ("fought_david", "false"),

        // s3_ending
        [0xD336EF2791C77C0E] = ("s3_ending", "kate"),
        [0x5850FFFD1553E6F1] = ("s3_ending", "david"),

        // gave_aj_medicine
        [0xD7311E0D3072E7C2] = ("gave_aj_medicine", "true"),
        [0xD006E8B18420EAAB] = ("gave_aj_medicine", "false"),

        // trippava_saved
        [0x65864B7A9C79F70F] = ("trippava_saved", "ava"),
        [0x37FD9A5893FC1E6D] = ("trippava_saved", "tripp"),

        // told_kate_feelings
        [0x89448D997F28AD95] = ("told_kate_feelings", "false"),
        [0x18CF1AA2FA2F2E9D] = ("told_kate_feelings", "true"),

        // richmond_entry
        [0xA36FCC3CE49952B8] = ("richmond_entry", "true"),
        [0x093A829547FBB966] = ("richmond_entry", "false"),

        // prescott_response
        [0x3D49F3E0C2946DBD] = ("prescott_response", "negotiate"),
        [0x81CD7133FD1B1C53] = ("prescott_response", "fire"),
        [0xDA5FFCB9438626FF] = ("prescott_response", "surrender"),

        // max_fate
        [0xC12355750B83CB58] = ("max_fate", "killed"),
        [0x522E1A31D01C7C6A] = ("max_fate", "spared"),

        // went_after_gabe
        [0x5EB3DB7A9EEB5030] = ("went_after_gabe", "gabe"),
        [0xECEEE227C2A55942] = ("went_after_gabe", "kate"),

        // stood_with_david
        [0x7AC877F8AC979DE7] = ("stood_with_david", "true"),
        [0xFC678F4328A1C9F5] = ("stood_with_david", "false"),

        // stayed_junkyard
        [0xE048284E2F044A6B] = ("stayed_junkyard", "true"),
        [0x323AD9BE15372581] = ("stayed_junkyard", "false"),

        // escaped_or_stayed
        [0xC5FBFD1BB7FEB50E] = ("escaped_or_stayed", "true"),
        [0x8C2E728E2EB17BA0] = ("escaped_or_stayed", "false"),

        // david_kate_argument
        [0x9A1E6D25587BA943] = ("david_kate_argument", "true"),
        [0x5BB8319FD8AA7E21] = ("david_kate_argument", "false"),

        // clem_came_along
        [0x5A0EEC8D2BFC7A28] = ("clem_came_along", "true"),
        [0x5604C125795B93BB] = ("clem_came_along", "false"),

        // badger_fate
        [0x928D9814DC2D081D] = ("badger_fate", "turn"),
        [0x044BA611E7C08DC8] = ("badger_fate", "someone_else"),
        [0x799C1E282E6239AC] = ("badger_fate", "quick"),
        [0x82C35BB20EB58C43] = ("badger_fate", "destroyed"),

        // shot_driver
        [0x89F4110E9AE2F540] = ("shot_driver", "true"),
        [0x24D1564E24B1B252] = ("shot_driver", "false"),

        // shooting_aftermath
        [0x89AA1B4F9A0BAA1A] = ("shooting_aftermath", "locked"),
        [0x8F3C70F1C3AF2776] = ("shooting_aftermath", "free"),

        // who_brought_junkyard
        [0x5F2F5CC45FEA6EFC] = ("who_brought_junkyard", "eleanor"),
        [0x4D18BBF11249F449] = ("who_brought_junkyard", "tripp"),
    };

    /// <summary>
    /// S4 node hash -> (choiceKey, optionValue).
    /// Placeholder: S4 hashes to be populated when extracted from choice.prop.
    /// </summary>
    public static readonly Dictionary<ulong, (string ChoiceKey, string OptionValue)> S4Nodes = new()
    {
        // S4 hashes to be added once extracted from choice.prop expression fields.
    };

    /// <summary>
    /// Michonne GUID -> (choiceKey, optionValue).
    /// Michonne uses GUIDs instead of CRC64 hashes in its choice.prop expressions.
    /// </summary>
    public static readonly Dictionary<string, (string ChoiceKey, string OptionValue)> MichonneNodes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // revealed_to_paige
        ["C58DCD3F-A1E3-4E84-AFEF-BBFC77B0207D"] = ("revealed_to_paige", "sympathy"),
        ["BD4CD905-2C29-43E7-B038-D42E6D6596BC"] = ("revealed_to_paige", "silent"),
        ["7A3E6A4C-9710-417A-B244-D5A7F912D3DE"] = ("revealed_to_paige", "disclosed"),
        ["EA2D1274-9FBA-4DE2-ADA8-E3C89AD1506E"] = ("revealed_to_paige", "advice"),

        // stayed_with_daughters
        ["7C97D402-B452-4F31-803B-165964D31B17"] = ("stayed_with_daughters", "true"),
        ["38E03171-5D18-4C77-8D5E-310366B27BAF"] = ("stayed_with_daughters", "false"),

        // told_alex_father
        ["A2ACEF40-7AF4-47D5-B5EE-0E7ABC04CAC0"] = ("told_alex_father", "later"),
        ["D8403477-ED6E-49D8-9FF1-8C510CB7CE91"] = ("told_alex_father", "silent"),
        ["914F6634-0411-4FF1-833D-953A3E48356A"] = ("told_alex_father", "dead"),
        ["C7925E58-E47C-4159-A432-E5F73D954D4B"] = ("told_alex_father", "hurt"),

        // ambushed_randall
        ["16CB7424-EB64-4389-A508-916A54EC6A4F"] = ("ambushed_randall", "true"),
        ["49209B47-B181-4F94-9708-9D78360E549F"] = ("ambushed_randall", "false"),

        // radio_norma
        ["47DE1BBC-D4FA-4699-8C9D-654A19FA1245"] = ("radio_norma", "ignore"),
        ["FEBA1DFC-42E6-4E70-9210-659E5EFC4C00"] = ("radio_norma", "spoke"),
        ["B69239AE-6A3A-4A3E-98C3-E20E88242F0E"] = ("radio_norma", "randall"),

        // picked_up_phone
        ["507F2CEF-BF23-4767-A130-F00C6E82F0AB"] = ("picked_up_phone", "true"),
        ["CE5585EA-0FA6-421C-ADDE-3701E8F8BFA3"] = ("picked_up_phone", "false"),

        // sold_greg_out
        ["514A197E-92FA-4266-858F-198376A52341"] = ("sold_greg_out", "shared"),
        ["C9DDB72B-2F35-4D49-9B40-52F169579299"] = ("sold_greg_out", "liar"),
        ["8AA59B5E-5C0F-4534-BC19-2749EC9B2158"] = ("sold_greg_out", "blame"),

        // let_sam_bury
        ["685515CD-32DA-4C50-BD48-6CB08444B8FE"] = ("let_sam_bury", "true"),
    };

    // Reverse lookup: (seasonKey, choiceKey, optionValue) -> hash or GUID

    private static readonly Dictionary<(string SeasonKey, string ChoiceKey, string OptionValue), ulong> _s3Reverse;
    private static readonly Dictionary<(string SeasonKey, string ChoiceKey, string OptionValue), ulong> _s4Reverse;
    private static readonly Dictionary<(string ChoiceKey, string OptionValue), string> _michonneReverse;

    static ChoiceNodeMapping()
    {
        _s3Reverse = new Dictionary<(string, string, string), ulong>();
        foreach (var (hash, (key, val)) in S3Nodes)
            _s3Reverse[("s3", key, val)] = hash;

        _s4Reverse = new Dictionary<(string, string, string), ulong>();
        foreach (var (hash, (key, val)) in S4Nodes)
            _s4Reverse[("s4", key, val)] = hash;

        _michonneReverse = new Dictionary<(string, string), string>();
        foreach (var (guid, (key, val)) in MichonneNodes)
            _michonneReverse[(key, val)] = guid;
    }

    /// <summary>
    /// Detect which choice was made based on a node hash from an EventLog record.
    /// Returns null if the hash is not recognized.
    /// </summary>
    public static (string ChoiceKey, string OptionValue)? DetectChoice(string seasonKey, ulong nodeHash)
    {
        var nodes = seasonKey.ToLowerInvariant() switch
        {
            "s3" => S3Nodes,
            "s4" => S4Nodes,
            _ => null,
        };

        if (nodes != null && nodes.TryGetValue(nodeHash, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Detect which Michonne choice was made based on a GUID string.
    /// Returns null if the GUID is not recognized.
    /// </summary>
    public static (string ChoiceKey, string OptionValue)? DetectMichonneChoice(string guid)
    {
        if (MichonneNodes.TryGetValue(guid, out var result))
            return result;
        return null;
    }

    /// <summary>
    /// Get the node hash for a given choice option. Used when writing a new choice value.
    /// Returns null if the mapping is not available.
    /// </summary>
    public static ulong? GetNodeHash(string seasonKey, string choiceKey, string optionValue)
    {
        var key = (seasonKey.ToLowerInvariant(), choiceKey, optionValue);

        return seasonKey.ToLowerInvariant() switch
        {
            "s3" => _s3Reverse.TryGetValue(key, out var h3) ? h3 : null,
            "s4" => _s4Reverse.TryGetValue(key, out var h4) ? h4 : null,
            _ => null,
        };
    }

    /// <summary>
    /// Get the GUID for a given Michonne choice option. Used when writing a new choice value.
    /// Returns null if the mapping is not available.
    /// </summary>
    public static string? GetMichonneGuid(string choiceKey, string optionValue)
    {
        return _michonneReverse.TryGetValue((choiceKey, optionValue), out var guid) ? guid : null;
    }
}
