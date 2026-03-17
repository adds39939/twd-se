using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Test if metadata CRC64 keys match known choice key names from ChoiceDatabase.
/// S1 metadata stores "carley", "duck", "lee" etc. — these are the VALUES of choices.
/// The KEY hashes might be CRC64 of the choice key names like "dougcarley_saved".
/// </summary>
public class MetadataKeyInvestigation2
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    [Fact]
    public void MatchChoiceKeysToMetadataHashes()
    {
        var lines = new List<string>();

        // Get all S1 metadata hashes
        var s1Metadata = GetMostCompleteMetadata("S1");
        if (s1Metadata == null) { Assert.Fail("No S1 metadata"); return; }

        lines.Add("=== S1 Metadata String properties ===");
        var stringProps = new Dictionary<ulong, string>();
        foreach (var g in s1Metadata.TypeGroups)
        {
            foreach (var p in g.Properties)
            {
                if (p.Value is StringValue sv)
                    stringProps[p.KeySymbol.Value] = sv.Value;
            }
        }
        foreach (var (hash, val) in stringProps.OrderBy(kv => kv.Key))
            lines.Add($"  0x{hash:X16} = \"{val}\"");

        // Now try matching against ALL known choice keys from ChoiceDatabase
        lines.Add("\n=== Testing choice keys from ChoiceDatabase ===");
        var choiceKeyHashes = new Dictionary<string, ulong>();
        foreach (var choice in ChoiceDatabase.AllChoices)
        {
            var hash = TelltaleHash.ComputeCrc64(choice.ChoiceKey);
            choiceKeyHashes[choice.ChoiceKey] = hash;

            if (stringProps.ContainsKey(hash))
                lines.Add($"  MATCH: '{choice.ChoiceKey}' -> 0x{hash:X16} = \"{stringProps[hash]}\"");
        }

        // Also try episode property names
        var episodeKeys = new[]
        {
            "episode_1_choices", "episode_2_choices", "episode_3_choices",
            "episode_4_choices", "episode_5_choices", "episode_400days_choices",
        };
        foreach (var key in episodeKeys)
        {
            var hash = TelltaleHash.ComputeCrc64(key);
            if (stringProps.ContainsKey(hash))
                lines.Add($"  MATCH: '{key}' -> 0x{hash:X16} = \"{stringProps[hash]}\"");
        }

        // Try S1 choice key patterns that might differ
        var additionalKeys = new[]
        {
            // From choices.prop known entries
            "lied_to_hershel", "shawnduck_choice", "sided_with_kenny",
            "gave_irene_gun", "dougcarley_saved",
            "helped_kill_larry", "shot_jolene", "chopped_leg",
            "kill_duck_choice", "left_lilly", "fought_kenny",
            "got_punched", "shot_beatrice", "christaomid_choice",
            "weapon_choice", "lost_temper", "saved_ben",
            "killed_zombie_boy", "brought_clementine_to_crawford",
            "killed_stephanie", "found_molly_videotape",
            "watched_molly_videotape", "found_crawford_pamphlet",
            "found_clementine_drawings", "molly_fate",
            "ben_asks_for_advice", "surrendered_cleaver",
            "threatened_or_lied_to_vernon", "sewer_slippery_slide",
            "prologue_clementine_zombies_killed", "prologue_saving_kenny",
            "lee_blocks_molly_first_punch", "lee_blocks_molly_second_punch",
            "cut_off_arm", "hid_bite", "reveal_bite",
            "party_lee_ben_kenny_christa_omid", "with_kenny", "with_ben",
            "with_christa_and_omid", "lee_grabbed_kennys_hand",
            "killed_campman", "looked_in_campman_bag",
            "final_zombie_interaction", "lee_cliffhanger_line",
            "clementine_shot_lee", "player_saw_belltower_stairs_tutorial",
            // 400 Days
            "shot_dan", "left_eddie", "left_nate", "lied_to_leland",
            "russell_went", "wyatt_went", "shel_went", "vince_went",
        };

        lines.Add("\n=== Testing all known S1 choice keys ===");
        int matches = 0;
        foreach (var key in additionalKeys)
        {
            var hash = TelltaleHash.ComputeCrc64(key);
            if (stringProps.ContainsKey(hash))
            {
                lines.Add($"  MATCH: '{key}' -> 0x{hash:X16} = \"{stringProps[hash]}\"");
                matches++;
            }
        }
        lines.Add($"  Total matches: {matches}/{additionalKeys.Length}");

        // Show unmatched metadata keys
        var allTried = new HashSet<ulong>(choiceKeyHashes.Values);
        foreach (var key in additionalKeys) allTried.Add(TelltaleHash.ComputeCrc64(key));
        foreach (var key in episodeKeys) allTried.Add(TelltaleHash.ComputeCrc64(key));

        lines.Add($"\n=== Unmatched S1 metadata String hashes ({stringProps.Count - matches}) ===");
        foreach (var (hash, val) in stringProps.Where(kv => !allTried.Contains(kv.Key)).OrderBy(kv => kv.Key))
            lines.Add($"  0x{hash:X16} = \"{val}\"");

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Since matching choice keys against metadata doesn't work,
    /// try to reverse-engineer by value matching.
    /// If metadata has value "carley", what S1 choice has option value "carley"?
    /// That tells us the hash key corresponds to "dougcarley_saved".
    /// Then we can check if our CRC64 of "dougcarley_saved" matches.
    /// If NOT, the hash might use a different function.
    /// </summary>
    [Fact]
    public void ReverseEngineerByValueMatching()
    {
        var lines = new List<string>();

        var s1Metadata = GetMostCompleteMetadata("S1");
        if (s1Metadata == null) { Assert.Fail("No S1 metadata"); return; }

        // Map metadata hash -> string value
        var metaStrings = new Dictionary<ulong, string>();
        foreach (var g in s1Metadata.TypeGroups)
            foreach (var p in g.Properties)
                if (p.Value is StringValue sv)
                    metaStrings[p.KeySymbol.Value] = sv.Value;

        // Map choice option values -> choice key names
        var valueToChoice = new Dictionary<string, List<string>>();
        foreach (var choice in ChoiceDatabase.AllChoices)
        {
            foreach (var opt in choice.Options)
            {
                if (!valueToChoice.ContainsKey(opt.Value))
                    valueToChoice[opt.Value] = [];
                valueToChoice[opt.Value].Add(choice.ChoiceKey);
            }
        }

        lines.Add("=== Matching metadata values to choice definitions ===");

        foreach (var (hash, val) in metaStrings.OrderBy(kv => kv.Key))
        {
            if (val is "true" or "false")
            {
                // Many choices use true/false - can't uniquely identify
                lines.Add($"  0x{hash:X16} = \"{val}\" (bool-like, multiple possible choices)");
                continue;
            }

            // Find which choice has this as an option value
            if (valueToChoice.TryGetValue(val, out var possibleKeys))
            {
                foreach (var choiceKey in possibleKeys)
                {
                    var expectedHash = TelltaleHash.ComputeCrc64(choiceKey);
                    var hashMatch = expectedHash == hash;
                    lines.Add($"  0x{hash:X16} = \"{val}\" -> choice='{choiceKey}' " +
                              $"expectedHash=0x{expectedHash:X16} {(hashMatch ? "MATCH!" : "NO MATCH")}");
                }
            }
            else
            {
                // Could be a filename or other non-choice value
                lines.Add($"  0x{hash:X16} = \"{val}\" (no matching choice option)");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Since our CRC64 might not match, test the hash function itself.
    /// Compare our CRC64 output for known strings against the hash values in the file.
    /// If the hash function is wrong, we need to find the right one.
    /// </summary>
    [Fact]
    public void TestHashFunction()
    {
        var lines = new List<string>();

        // Known: 0xF235E9FCE9562E01 maps to a string value like "_wd1_saveslot1_autosave.bundle"
        // Known: 0xB218E7C003A67CE9 maps to a string value like "WalkingDead103"
        // These are the KEY hashes, not the value hashes.
        // The keys are CRC64 of the PROPERTY NAME, not the value.

        // Let's see what our hash function produces for various strings
        var testStrings = new[]
        {
            "dougcarley_saved", "shawnduck_choice", "christaomid_choice",
            "weapon_choice", "kill_duck_choice", "lied_to_hershel",
            "autosave_file", "playtime", "episode",
            // full qualified style
            "SaveSlotInfo::mAutosaveFile", "SaveSlotInfo::mPlaytime",
        };

        lines.Add("=== Our CRC64 output for test strings ===");
        foreach (var s in testStrings)
            lines.Add($"  CRC64(\"{s}\") = 0x{TelltaleHash.ComputeCrc64(s):X16}");

        // Check if ANY of our known choice key hashes match ANY metadata hashes
        var allMetaHashes = new HashSet<ulong>();
        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var meta = GetMostCompleteMetadata(season);
            if (meta == null) continue;
            foreach (var g in meta.TypeGroups)
                foreach (var p in g.Properties)
                    allMetaHashes.Add(p.KeySymbol.Value);
        }

        lines.Add($"\n=== Total unique metadata hashes across all seasons: {allMetaHashes.Count} ===");

        // Try ALL choice keys
        lines.Add("\n=== All choice key hashes ===");
        foreach (var choice in ChoiceDatabase.AllChoices)
        {
            var hash = TelltaleHash.ComputeCrc64(choice.ChoiceKey);
            var inMeta = allMetaHashes.Contains(hash);
            if (inMeta)
                lines.Add($"  '{choice.ChoiceKey}' -> 0x{hash:X16} IN METADATA!");
        }

        // Also check: does the metadata parent symbol hash tell us anything?
        lines.Add("\n=== Metadata parent symbols ===");
        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var meta = GetMostCompleteMetadata(season);
            if (meta == null) continue;
            foreach (var parent in meta.ParentSymbols)
                lines.Add($"  {season}: parent=0x{parent.Value:X16}");
        }

        // Try hashing possible parent class names
        var parentCandidates = new[]
        {
            "SaveSlotInfo", "saveslotinfo", "save_slot_info",
            "SaveSlotData", "SlotInfo", "SlotData",
            "GameSaveData", "GameSave", "SaveData",
            "MenuSaveSlotInfo", "WalkingDeadSaveSlotInfo",
            "WDSaveSlotInfo", "WD1SaveSlotInfo", "WD2SaveSlotInfo",
            "MetadataSlot", "metadata_slot", "MetadataSlotInfo",
        };

        lines.Add("\n=== Parent symbol hash attempts ===");
        foreach (var c in parentCandidates)
        {
            var hash = TelltaleHash.ComputeCrc64(c);
            lines.Add($"  CRC64(\"{c}\") = 0x{hash:X16}");
        }

        // Actual parent hashes:
        // S1: 0xF79925AC5D1766F2
        // S2: 0xA9710CEFE29AE8FD
        // S3: 0x5D794B77EF478089
        // S4: ? (need to check)
        // Michonne: ? (need to check)

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// The nuclear option: if the hash function or naming is different,
    /// try searching the TelltaleToolLib HashDB online for our target hashes.
    /// </summary>
    [Fact]
    public void SearchTelltaleHashDB()
    {
        var lines = new List<string>();

        // Known target hashes we need to crack
        var targets = new (ulong hash, string context)[]
        {
            (0x7C725227A47FD1BA, "int32 playtime-like (31-178)"),
            (0xB218E7C003A67CE9, "episode identifier (WalkingDead103 or int 1-5)"),
            (0xF235E9FCE9562E01, "autosave filename string"),
            (0xFD50E3BE7B29A8B1, "int32 max episode (1-4)"),
            (0x4F8338150CC8BCD6, "bool present in S1/S3/S4/Michonne"),
            (0x88921A29F6F6E763, "bool present in S1/S3/S4/Michonne"),
            (0x28990F8E3B72C32C, "S1 String=carley (dougcarley_saved?)"),
            (0x2BBCEEAEBC43E226, "S1 String=duck (shawnduck_choice?)"),
            (0x1246D403ADB9423B, "S1 String=lee (kill_duck_choice?)"),
            (0xD6F0F44969B9B90E, "S1 String=omid (christaomid_choice?)"),
            (0x908131F693A08879, "S1 String='inventory - spike remover' (weapon_choice?)"),
        };

        lines.Add("=== Target hashes with computed CRC64 for suspected names ===");
        foreach (var (hash, context) in targets)
        {
            lines.Add($"  Target: 0x{hash:X16} — {context}");
        }

        // Show what our CRC64 produces for the suspected names
        var pairs = new (ulong target, string suspected)[]
        {
            (0x28990F8E3B72C32C, "dougcarley_saved"),
            (0x2BBCEEAEBC43E226, "shawnduck_choice"),
            (0x1246D403ADB9423B, "kill_duck_choice"),
            (0xD6F0F44969B9B90E, "christaomid_choice"),
            (0x908131F693A08879, "weapon_choice"),
        };

        lines.Add("\n=== Hash comparison ===");
        foreach (var (target, name) in pairs)
        {
            var computed = TelltaleHash.ComputeCrc64(name);
            lines.Add($"  0x{target:X16} (actual) vs 0x{computed:X16} (CRC64(\"{name}\")) — {(target == computed ? "MATCH" : "DIFFERENT")}");
        }

        // If they don't match, try different hash variations:
        // 1. CRC64 without lowercasing
        // 2. CRC64 with different polynomial
        // 3. Simple string hash functions

        lines.Add("\n=== Alternative hash tests ===");
        foreach (var (target, name) in pairs)
        {
            // Try raw CRC64 without lowercasing (case-sensitive)
            var rawBytes = System.Text.Encoding.UTF8.GetBytes(name);
            var crc = new System.IO.Hashing.Crc64();
            crc.Append(rawBytes);
            var rawHash = crc.GetCurrentHashAsUInt64();
            lines.Add($"  CRC64_raw(\"{name}\") = 0x{rawHash:X16} {(rawHash == target ? "MATCH!" : "")}");

            // Try uppercase
            rawBytes = System.Text.Encoding.UTF8.GetBytes(name.ToUpper());
            crc = new System.IO.Hashing.Crc64();
            crc.Append(rawBytes);
            rawHash = crc.GetCurrentHashAsUInt64();
            lines.Add($"  CRC64_upper(\"{name}\") = 0x{rawHash:X16} {(rawHash == target ? "MATCH!" : "")}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private PropertySet? GetMostCompleteMetadata(string season)
    {
        var dir = Path.Combine(SaveDir, season);
        if (!Directory.Exists(dir)) return null;

        var file = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
        if (file == null) return null;

        return BundleReader.Read(file).Metadata;
    }
}
