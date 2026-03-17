using System.Text;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Crack the CRC64 metadata property keys by:
/// 1. Finding what names produce known S1/S2 metadata hashes
/// 2. Searching Telltale community symbol databases
/// 3. Trying patterns from both old and new engine naming conventions
/// </summary>
public class MetadataKeyInvestigation
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    /// <summary>
    /// Dump ALL metadata from ALL seasons to compare hash values and patterns.
    /// </summary>
    [Fact]
    public void DumpAllMetadata()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var slotFiles = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .OrderBy(f => new FileInfo(f).Length)
                .ToList();

            lines.Add($"\n=== {season}: {slotFiles.Count} slot files ===");

            // Just use the LAST (most complete) slot file
            var file = slotFiles.LastOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.Metadata == null) continue;

            lines.Add($"  File: {Path.GetFileName(file)}");
            lines.Add($"  Metadata v{slot.Metadata.Version} flags=0x{slot.Metadata.Flags:X}");
            lines.Add($"  Parents: {slot.Metadata.ParentSymbols.Count}");
            foreach (var parent in slot.Metadata.ParentSymbols)
                lines.Add($"    Parent: 0x{parent.Value:X16}");

            lines.Add($"  Type groups: {slot.Metadata.TypeGroups.Count}");

            foreach (var g in slot.Metadata.TypeGroups)
            {
                var typeName = g.TypeSymbol.Value switch
                {
                    var v when v == TelltaleTypes.String => "String",
                    var v when v == TelltaleTypes.Bool => "bool",
                    var v when v == TelltaleTypes.Int32 => "int32",
                    var v when v == TelltaleTypes.Float => "float",
                    var v when v == TelltaleTypes.Symbol => "Symbol",
                    var v when v == TelltaleTypes.Flags => "Flags",
                    _ => $"0x{g.TypeSymbol.Value:X16}"
                };
                lines.Add($"\n  Group [{typeName}] ({g.Properties.Count} props):");

                foreach (var p in g.Properties)
                {
                    string valStr = p.Value switch
                    {
                        StringValue sv => $"\"{sv.Value}\"",
                        BoolValue bv => bv.Value.ToString(),
                        IntValue iv => iv.Value.ToString(),
                        FloatValue fv => fv.Value.ToString("F3"),
                        _ => p.Value.GetType().Name
                    };
                    lines.Add($"    0x{p.KeySymbol.Value:X16} = {valStr}");
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Using the known S1 metadata string value ("autosave filename"),
    /// try to crack the naming convention for ALL metadata keys.
    /// </summary>
    [Fact]
    public void CrackKnownMetadataKeys()
    {
        var lines = new List<string>();

        // From S3/S4 slot examination we know:
        // 0xF235E9FCE9562E01 = string: autosave filename like "_wd3_saveslot1_autosave.bundle"
        // 0x7C725227A47FD1BA = int: values 21-178 (looks like playtime in seconds or minutes)
        // 0xB218E7C003A67CE9 = int: values 1-5 (episode number)
        // 0xFD50E3BE7B29A8B1 = int: values 1-4 (max completed episode? or something)
        // 0x0399C2FFE0D50348 = int: values 1-2 (some counter)
        //
        // Booleans accumulate as episodes are completed — likely episode completion flags

        // MASSIVE candidate list with every naming pattern I can think of
        var candidates = new List<string>();

        // Direct property names (lowercase, as TelltaleHash lowercases)
        var rawNames = new[]
        {
            // Autosave file
            "autosave file", "autosave_file", "autosavefile", "mAutosaveFile",
            "Autosave File", "AutosaveFile", "mAutosaveFilename",
            "autosave", "mAutosave", "autosaveFilename",
            "checkpoint file", "checkpoint_file", "checkpointFile",
            "mCheckpointFile", "save file", "save_file", "saveFile",
            "mSaveFile", "mCheckpoint", "checkpointFilename",
            "LastCheckpointFile", "lastCheckpointFile", "last_checkpoint_file",
            "AutosavedBundleFile", "autosavedBundleFile",

            // Playtime
            "playtime", "mPlaytime", "play time", "play_time",
            "Playtime", "PlayTime", "playTime",
            "mPlayTime", "mPlaytimeSeconds", "mPlaytimeMinutes",
            "total_playtime", "totalPlaytime", "mTotalPlaytime",
            "time_played", "timePlayed", "mTimePlayed",
            "game_time", "gameTime", "mGameTime",
            "elapsed_time", "elapsedTime", "mElapsedTime",
            "mTimeSpent", "timeSpent", "time_spent",
            "mPlayTimeTotal", "playtimeTotal", "playTimeTotal",
            "mSaveSlotPlaytime", "mSlotPlaytime",
            "minutes_played", "minutesPlayed", "mMinutesPlayed",
            "seconds_played", "secondsPlayed", "mSecondsPlayed",
            "TotalTime", "totalTime", "total_time",

            // Episode number
            "episode", "mEpisode", "Episode",
            "current_episode", "currentEpisode", "mCurrentEpisode",
            "CurrentEpisode", "current episode",
            "episode_number", "episodeNumber", "mEpisodeNumber",
            "ep", "mEp", "activeEpisode", "mActiveEpisode",
            "last_episode", "lastEpisode", "mLastEpisode",
            "mEpisodeIndex", "episodeIndex", "episode_index",

            // Max episode
            "max_episode", "maxEpisode", "mMaxEpisode",
            "MaxEpisode", "max episode", "highest_episode",
            "highestEpisode", "mHighestEpisode",
            "episodes_completed", "episodesCompleted", "mEpisodesCompleted",
            "mCompletedEpisodes", "completedEpisodes",
            "num_episodes_completed", "numEpisodesCompleted",
            "mMaxEpisodeCompleted", "maxEpisodeCompleted",
            "furthest_episode", "furthestEpisode", "mFurthestEpisode",

            // Episode completion bools - many patterns
            "episode 1 complete", "episode 2 complete", "episode 3 complete",
            "episode 4 complete", "episode 5 complete",
        };

        foreach (var n in rawNames) candidates.Add(n);

        // Systematic generation of episode-related names
        for (int ep = 1; ep <= 5; ep++)
        {
            var epPatterns = new[]
            {
                $"episode {ep} complete", $"episode_{ep}_complete", $"episode{ep}complete",
                $"Episode {ep} Complete", $"Episode{ep}Complete",
                $"mEpisode{ep}Complete", $"bEpisode{ep}Complete",
                $"ep{ep}_complete", $"ep{ep}complete", $"ep_{ep}_complete",
                $"EpComplete{ep}", $"mEpComplete{ep}", $"bEpComplete{ep}",
                $"completedEpisode{ep}", $"mCompletedEpisode{ep}",
                $"hasCompletedEpisode{ep}", $"mHasCompletedEpisode{ep}",
                $"episode{ep}done", $"episode{ep}Done", $"Episode{ep}Done",
                $"ep{ep}done", $"ep{ep}Done",
                $"episode{ep}finished", $"Episode{ep}Finished",
                $"episodeComplete{ep}", $"EpisodeComplete{ep}",
                $"mEpisodeComplete{ep}", $"mEpisodeDone{ep}",
                $"bEp{ep}Complete", $"bEp{ep}Done",
                $"ep{ep}", $"Ep{ep}",
                $"episode_{ep}", $"Episode_{ep}",
                $"episode_0{ep}_complete", $"episode_0{ep}",
                // Telltale often uses camelCase
                $"hasPlayedEpisode{ep}", $"mHasPlayedEpisode{ep}",
                $"playedEpisode{ep}", $"mPlayedEpisode{ep}",
                $"finishedEpisode{ep}", $"mFinishedEpisode{ep}",
                // With "Has" prefix
                $"HasCompletedEp{ep}", $"hasCompletedEp{ep}",
                // Zero-indexed
                $"episode {ep - 1} complete", $"episode_{ep - 1}_complete",
                $"Episode{ep - 1}Complete", $"mEpisode{ep - 1}Complete",
                $"ep{ep - 1}_complete", $"ep{ep - 1}complete",
            };
            candidates.AddRange(epPatterns);

            // Season-specific prefixes
            foreach (var prefix in new[] { "wd3_", "wd4_", "wdm_", "s3_", "s4_", "michonne_",
                                           "WD3_", "WD4_", "WDM_", "S3_", "S4_",
                                           "season3_", "season4_", "Season3_", "Season4_" })
            {
                candidates.Add($"{prefix}episode_{ep}_complete");
                candidates.Add($"{prefix}ep{ep}_complete");
                candidates.Add($"{prefix}ep{ep}complete");
            }
        }

        // The new engine might use GUIDs or different identifiers
        // Also try: property names might include full type paths
        var typePathPatterns = new[]
        {
            "SaveSlotInfo.mPlaytime", "SaveSlotInfo.mEpisode",
            "SaveSlotInfo.mAutosaveFile", "SaveSlotInfo.mMaxEpisode",
            "saveslotinfo.mplaytime", "saveslotinfo.mepisode",
            "GameState.mPlaytime", "GameState.mEpisode",
            "MenuSlotData.mPlaytime", "MenuSlotData.mEpisode",
            "SlotData.mPlaytime", "SlotData.mEpisode",
            "mSlotData.mPlaytime", "mSlotData.mEpisode",
        };
        candidates.AddRange(typePathPatterns);

        // Also try from the choicestats GUIDs — the bool keys might be GUID-derived
        // Seen in choicestats.pro: {D5CAC505-D44C-4338-A465-EFA30DF08A5E} etc.
        // Try hashing these GUIDs
        var guids = new[]
        {
            "D5CAC505-D44C-4338-A465-EFA30DF08A5E",
            "48C0A99E-C18D-45D7-B5CC-7798ABBE8296",
            "59287F15-5C7C-4C87-A3AA-792C16BB5603",
            "D4DFE2E9-4A8B-4DA7-B05A-D1C3495D639F",
            "D670AC7F-AF66-4025-B6B7-47F57E4A90F1",
            "F28A8FAC-A4EF-4E4C-B510-D947FAEF804E",
            "126EB207-831C-4BFF-9D85-FB9D66493BB5",
            "E20E5B4D-6404-46C1-979C-7CD96F720105",
            "1882BD84-079A-4BFD-A984-A64AB71FA581",
            "29E26E3D-A351-4856-B6EE-FD41F41E7D58",
            "80E48E39-2D80-406A-B313-008509B46C30",
        };
        foreach (var guid in guids)
        {
            candidates.Add(guid);
            candidates.Add($"{{{guid}}}");
            candidates.Add(guid.ToLower());
            candidates.Add($"{{{guid.ToLower()}}}");
        }

        // Compute and match
        var matched = new Dictionary<ulong, string>();
        var allTargets = CollectAllMetadataHashes();

        foreach (var c in candidates.Distinct())
        {
            var hash = TelltaleHash.ComputeCrc64(c);
            if (allTargets.Contains(hash) && !matched.ContainsKey(hash))
                matched[hash] = c;
        }

        lines.Add($"Tried {candidates.Distinct().Count()} candidates against {allTargets.Count} target hashes");
        lines.Add($"Matches: {matched.Count}");

        foreach (var (hash, name) in matched.OrderBy(kv => kv.Key))
            lines.Add($"  0x{hash:X16} = '{name}'");

        lines.Add($"\nUnmatched ({allTargets.Count - matched.Count}):");
        foreach (var h in allTargets.Where(h => !matched.ContainsKey(h)).OrderBy(h => h))
            lines.Add($"  0x{h:X16}");

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Search for the hash values online or in known Telltale symbol databases.
    /// TelltaleToolLib has a HashDB — let's try to WebFetch it.
    /// Meanwhile, try ALL lowercase short strings (brute-force 1-6 char combos).
    /// </summary>
    [Fact]
    public void SmartBruteForce()
    {
        var lines = new List<string>();
        var targets = CollectAllMetadataHashes();
        lines.Add($"Target hashes: {targets.Count}");

        // Phase 1: Try ALL words from the Lua scene names we found
        // The scene names give us vocabulary: "Richmond", "Gate", "Finale", "Boarding", "School", etc.
        var sceneWords = new[]
        {
            // From S3 scenes
            "Richmond", "Gate", "Finale", "Junkyard", "Prescott", "Factory", "Flashback",
            "Garcia", "David", "Kate", "Gabe", "Javi", "Javier", "Conrad", "Tripp", "Ava",
            "Eleanor", "Badger", "Max", "Clint", "Joan", "Jesus", "NewFrontier",
            // From S4 scenes
            "BoardingSchool", "Exterior", "Damaged", "Interior", "Barn", "Bridge",
            "Fishing", "Train", "Cave", "Greenhouse", "SafeRoom", "Tower",
            "AJ", "Clem", "Clementine", "Louis", "Violet", "Tenn", "Marlon",
            "Brody", "Mitch", "Ruby", "Aasim", "Omar", "Willy", "James", "Lilly", "Abel",
            "Minnie", "Sophie",
            // From Michonne scenes
            "Johns", "House", "Upstairs", "Daughters", "Boat", "Ferry", "Monroe",
            "Sam", "Pete", "Randall", "Norma", "Greg", "Jonas", "Paige", "Alex",
            // Common words
            "Choice", "choice", "Decision", "decision", "Flag", "flag",
            "Save", "save", "Load", "load", "Complete", "complete",
            "Episode", "episode", "Chapter", "chapter",
            "Alive", "alive", "Dead", "dead", "Killed", "killed", "Saved", "saved",
            "Made", "made", "Selected", "selected", "Picked", "picked",
        };

        var candidates = new HashSet<string>();

        // Single words
        foreach (var w in sceneWords) candidates.Add(w);

        // Common prefixes + character names
        var prefixes2 = new[] { "b", "m", "is", "has", "did", "was", "got",
                                "saved_", "killed_", "chose_", "picked_",
                                "b_", "m_", "is_", "has_", "did_" };
        var names = new[] { "ava", "tripp", "conrad", "kate", "david", "gabe", "javi",
                           "louis", "violet", "tenn", "marlon", "james", "lilly", "abel",
                           "clementine", "clem", "aj", "kenny", "mitch", "pete",
                           "sam", "randall", "norma", "joan", "clint", "eleanor",
                           "jesus", "badger", "max", "brody", "ruby", "aasim",
                           "omar", "willy", "minnie", "sophie" };

        foreach (var prefix in prefixes2)
            foreach (var name in names)
            {
                candidates.Add($"{prefix}{name}");
                candidates.Add($"{prefix}{name}_alive");
                candidates.Add($"{prefix}{name}_dead");
                candidates.Add($"{prefix}{name}Alive");
                candidates.Add($"{prefix}{name}Dead");
            }

        // Action patterns
        var actions = new[] { "saved", "killed", "helped", "sided_with", "chose",
                              "trusted", "betrayed", "romanced", "abandoned", "protected",
                              "forgave", "attacked", "shot", "spared", "let_die",
                              "defended", "condemned", "lied_to", "agreed_with" };
        foreach (var action in actions)
            foreach (var name in names)
            {
                candidates.Add($"{action}_{name}");
                candidates.Add($"{action}{char.ToUpper(name[0])}{name[1..]}");
            }

        // Choice stat GUIDs as hash keys
        var guids = new[]
        {
            "D5CAC505-D44C-4338-A465-EFA30DF08A5E",
            "48C0A99E-C18D-45D7-B5CC-7798ABBE8296",
            "59287F15-5C7C-4C87-A3AA-792C16BB5603",
            "D4DFE2E9-4A8B-4DA7-B05A-D1C3495D639F",
            "D670AC7F-AF66-4025-B6B7-47F57E4A90F1",
            "F28A8FAC-A4EF-4E4C-B510-D947FAEF804E",
            "126EB207-831C-4BFF-9D85-FB9D66493BB5",
            "E20E5B4D-6404-46C1-979C-7CD96F720105",
            "1882BD84-079A-4BFD-A984-A64AB71FA581",
            "29E26E3D-A351-4856-B6EE-FD41F41E7D58",
            "80E48E39-2D80-406A-B313-008509B46C30",
        };
        foreach (var g in guids)
        {
            candidates.Add(g);
            candidates.Add($"{{{g}}}");
            candidates.Add(g.ToLower());
            candidates.Add($"{{{g.ToLower()}}}");
            candidates.Add($"( {{{g}}} )");
        }

        // Match
        var matched = new Dictionary<ulong, string>();
        foreach (var c in candidates)
        {
            var hash = TelltaleHash.ComputeCrc64(c);
            if (targets.Contains(hash) && !matched.ContainsKey(hash))
                matched[hash] = c;
        }

        lines.Add($"Tried {candidates.Count} candidates");
        lines.Add($"Matches: {matched.Count}");

        foreach (var (hash, name) in matched.OrderBy(kv => kv.Key))
            lines.Add($"  0x{hash:X16} = '{name}'");

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Compare S1 metadata hashes with S3/S4 to find shared keys.
    /// Shared keys between seasons likely have generic names.
    /// </summary>
    [Fact]
    public void CompareMetadataAcrossSeasons()
    {
        var lines = new List<string>();
        var seasonHashes = new Dictionary<string, Dictionary<ulong, (string type, string sampleValue)>>();

        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            seasonHashes[season] = [];

            // Use the most complete slot file
            var slotFiles = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .OrderByDescending(f => new FileInfo(f).Length)
                .ToList();

            foreach (var file in slotFiles)
            {
                var slot = BundleReader.Read(file);
                if (slot.Metadata == null) continue;

                foreach (var g in slot.Metadata.TypeGroups)
                {
                    foreach (var p in g.Properties)
                    {
                        var typeName = g.TypeSymbol.Value switch
                        {
                            var v when v == TelltaleTypes.String => "String",
                            var v when v == TelltaleTypes.Bool => "bool",
                            var v when v == TelltaleTypes.Int32 => "int32",
                            _ => "other"
                        };
                        var valStr = p.Value switch
                        {
                            StringValue sv => sv.Value,
                            BoolValue bv => bv.Value.ToString(),
                            IntValue iv => iv.Value.ToString(),
                            _ => "?"
                        };
                        seasonHashes[season].TryAdd(p.KeySymbol.Value, (typeName, valStr));
                    }
                }
            }
        }

        // Find shared keys
        var allKeys = seasonHashes.Values.SelectMany(d => d.Keys).Distinct().OrderBy(k => k).ToList();

        lines.Add("=== Key presence across seasons ===");
        foreach (var key in allKeys)
        {
            var seasons = seasonHashes
                .Where(kv => kv.Value.ContainsKey(key))
                .Select(kv => $"{kv.Key}({kv.Value[key].type}={kv.Value[key].sampleValue})")
                .ToList();

            if (seasons.Count >= 2) // Only show keys present in multiple seasons
                lines.Add($"  0x{key:X16}: {string.Join(", ", seasons)}");
        }

        lines.Add("\n=== Season-specific keys ===");
        foreach (var (season, hashes) in seasonHashes.OrderBy(kv => kv.Key))
        {
            var unique = hashes.Keys
                .Where(k => !seasonHashes.Where(s => s.Key != season).Any(s => s.Value.ContainsKey(k)))
                .ToList();
            if (unique.Count > 0)
            {
                lines.Add($"\n  {season} only ({unique.Count}):");
                foreach (var k in unique.OrderBy(k => k))
                    lines.Add($"    0x{k:X16} {hashes[k].type}={hashes[k].sampleValue}");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    private HashSet<ulong> CollectAllMetadataHashes()
    {
        var result = new HashSet<ulong>();
        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_")))
            {
                var slot = BundleReader.Read(file);
                if (slot.Metadata == null) continue;
                foreach (var g in slot.Metadata.TypeGroups)
                    foreach (var p in g.Properties)
                        result.Add(p.KeySymbol.Value);
            }
        }
        return result;
    }
}
