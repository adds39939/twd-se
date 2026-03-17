using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Part 2: Verify scene format, decode metadata bool keys, parse choicestats.pro
/// </summary>
public class SceneFormatInvestigation2
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    /// <summary>
    /// Verify the scene save format by parsing with u32 block-size skipping.
    /// Format: u32(entry_count) + per_entry(u32(name_len) + name + u32(state_size) + state_data)
    /// </summary>
    [Fact]
    public void VerifySceneSaveFormat()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

            var defData = ExtractDefaultSection(raw);
            if (defData == null) continue;

            lines.Add($"\n=== {season}: {Path.GetFileName(file)} ({defData.Length} bytes) ===");

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);
            var entryCount = br.ReadUInt32();
            lines.Add($"  Entry count: {entryCount}");

            int parsed = 0;
            for (uint i = 0; i < entryCount && ms.Position < ms.Length - 4; i++)
            {
                var strLen = br.ReadInt32();
                if (strLen <= 0 || strLen > 500 || ms.Position + strLen > ms.Length) break;
                var name = Encoding.ASCII.GetString(br.ReadBytes(strLen));

                if (ms.Position + 4 > ms.Length) break;
                var blockSize = br.ReadUInt32();
                if (blockSize > ms.Length - ms.Position) break;

                if (i < 3 || i == entryCount - 1)
                    lines.Add($"  [{i}] '{name}' state={blockSize} bytes");

                ms.Position += blockSize;
                parsed++;
            }

            var remaining = ms.Length - ms.Position;
            lines.Add($"  Parsed {parsed}/{entryCount} entries OK, {remaining} bytes remaining");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Brute-force match the CRC64 metadata bool keys from S3/S4 slot files
    /// against known choice-related strings.
    /// </summary>
    [Fact]
    public void BruteForceMetadataBoolKeys()
    {
        var lines = new List<string>();

        // Collect all unique bool key hashes from S3/S4/Michonne slot files
        var allBoolKeys = new Dictionary<string, HashSet<ulong>>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var slotFiles = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            allBoolKeys[season] = [];

            foreach (var file in slotFiles)
            {
                var slot = BundleReader.Read(file);
                if (slot.Metadata == null) continue;

                foreach (var g in slot.Metadata.TypeGroups)
                {
                    foreach (var p in g.Properties)
                    {
                        if (p.Value is BoolValue)
                            allBoolKeys[season].Add(p.KeySymbol.Value);
                    }
                }
            }

            lines.Add($"{season}: {allBoolKeys[season].Count} unique bool keys across {slotFiles.Count} slot files");
        }

        // Known metadata keys (from S1 saves)
        var knownKeys = new Dictionary<string, ulong>();
        // Try a big list of possible key names
        var candidates = new List<string>
        {
            // Known metadata keys
            "episode", "slot", "autosave_file", "playtime",
            "episode_1_complete", "episode_2_complete", "episode_3_complete",
            "episode_4_complete", "episode_5_complete",
            "ep1_complete", "ep2_complete", "ep3_complete", "ep4_complete", "ep5_complete",
            "episode1_complete", "episode2_complete", "episode3_complete",
            "episode4_complete", "episode5_complete",
            "episode_complete_1", "episode_complete_2", "episode_complete_3",
            "ep_complete_1", "ep_complete_2", "ep_complete_3",
            "completed_episode_1", "completed_episode_2", "completed_episode_3",
            "completed_episode_4", "completed_episode_5",
            "mPlaytime", "mEpisode", "mSlot",
            // S3 specific
            "wd3_episode_1_complete", "wd3_episode_2_complete",
            "wd3_ep1_complete", "wd3_ep2_complete",
            // Choice-related patterns
            "choice_made", "made_choice", "decision_made",
            // S3 character names / choices
            "saved_ava", "saved_tripp", "ava_alive", "tripp_alive",
            "ava_dead", "tripp_dead", "shot_conrad", "killed_conrad",
            "conrad_alive", "conrad_dead", "sided_with_joan", "sided_with_richmond",
            "accepted_new_frontier", "refused_new_frontier",
            "gave_blood", "kept_blood",
            "david_alive", "kate_alive", "gabe_alive",
            "david_dead", "kate_dead", "gabe_dead",
            // S3 endings
            "ending_kate", "ending_david", "ending_alone",
            "went_with_kate", "went_with_david", "went_alone",
            "saved_richmond", "abandoned_richmond",
            // S4 character names / choices
            "saved_louis", "saved_violet", "louis_alive", "violet_alive",
            "louis_dead", "violet_dead", "trusted_aj", "shot_lilly",
            "killed_lilly", "lilly_alive", "lilly_dead",
            "let_aj_kill_lilly", "stopped_aj",
            "aj_shot_tenn", "aj_saved_tenn", "tenn_alive", "tenn_dead",
            "romance_louis", "romance_violet", "romanced_louis", "romanced_violet",
            "let_abel_turn", "killed_abel",
            "trusted_james", "betrayed_james", "james_alive", "james_dead",
            "marlon_killed", "marlon_alive",
            "aj_killed_marlon", "defended_aj", "condemned_aj",
            "clem_alive", "clem_survived",
            // Michonne character names / choices
            "killed_norma", "killed_randall", "norma_alive", "randma_alive",
            "gave_sam_shelter", "took_children",
            "pete_alive", "pete_dead", "saved_pete",
            // Generic patterns that Telltale might use
            "has_made_choice", "choice_1", "choice_2", "choice_3",
            "choice_ep1", "choice_ep2", "choice_ep3",
            "major_choice_1", "major_choice_2",
            // Bool flag patterns (common in Telltale)
            "flag_", "bHas", "bIs", "bDid",
            "bHasCompleted", "bIsComplete",
        };

        // Also try common patterns with prefixes
        var prefixes = new[] { "", "b", "m", "save_", "flag_", "meta_", "stat_", "game_", "wd3_", "wd4_", "wdm_" };
        var basenames = new[]
        {
            "EpisodeComplete", "EpisodeDone", "HasPlayed", "FinishedEpisode",
            "MadeChoice", "ChoiceMade", "HasChoice",
            "SavedAva", "SavedTripp", "KilledConrad", "ShotConrad",
            "AvaAlive", "TrippAlive", "ConradAlive",
            "SavedLouis", "SavedViolet", "TrustedAJ", "ShotLilly",
            "LouisAlive", "VioletAlive", "TennAlive", "JamesAlive",
            "AJKilledMarlon", "DefendedAJ", "CondemnedAJ",
            "RomancedLouis", "RomancedViolet",
        };

        foreach (var prefix in prefixes)
            foreach (var name in basenames)
                candidates.Add(prefix + name);

        // Compute CRC64 for all candidates
        var hashToName = new Dictionary<ulong, string>();
        foreach (var candidate in candidates.Distinct())
        {
            var hash = TelltaleHash.ComputeCrc64(candidate);
            hashToName[hash] = candidate;
        }

        lines.Add($"\nTried {hashToName.Count} candidate strings");

        // Check matches
        foreach (var (season, keys) in allBoolKeys)
        {
            lines.Add($"\n{season} bool keys:");
            foreach (var key in keys.OrderBy(k => k))
            {
                var matched = hashToName.TryGetValue(key, out var name);
                lines.Add($"  0x{key:X16} = {(matched ? $"MATCH: '{name}'" : "unknown")}");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Parse choicestats.pro from S4 slot files.
    /// </summary>
    [Fact]
    public void ParseChoiceStatsPro()
    {
        var lines = new List<string>();
        var psReader = new PropertySetReader();

        foreach (var season in new[] { "S4" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var slotFiles = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            foreach (var file in slotFiles)
            {
                var slot = BundleReader.Read(file);
                if (slot.RawInnerFiles?.TryGetValue("choicestats.pro", out var raw) != true) continue;

                lines.Add($"\n{Path.GetFileName(file)}: choicestats.pro ({raw.Length} bytes)");
                lines.Add($"  First 64: {BitConverter.ToString(raw.Take(64).ToArray())}");

                // Try parsing as MetaStream -> PropertySet
                try
                {
                    var defData = ExtractInnerDefaultSection(raw);
                    if (defData == null) { lines.Add("  Could not extract default section"); continue; }

                    lines.Add($"  Default section: {defData.Length} bytes");
                    var ps = psReader.Read(defData);
                    lines.Add($"  PropertySet v{ps.Version} flags=0x{ps.Flags:X} parents={ps.ParentSymbols.Count} groups={ps.TypeGroups.Count}");

                    foreach (var g in ps.TypeGroups)
                    {
                        var typeName = g.TypeSymbol.Value switch
                        {
                            0xCD9C6E605F5AF4B4 => "String",
                            0x9004C5587575D6C0 => "bool",
                            0x7CACEEBCD26D075C => "int32",
                            _ => $"0x{g.TypeSymbol.Value:X16}"
                        };
                        lines.Add($"  Group {typeName}: {g.Properties.Count} props");

                        foreach (var p in g.Properties)
                        {
                            string valStr = p.Value switch
                            {
                                StringValue sv => $"\"{sv.Value}\"",
                                BoolValue bv => bv.Value.ToString(),
                                IntValue iv => iv.Value.ToString(),
                                SymbolValue symv => $"Symbol(0x{symv.Value.Value:X16})",
                                RawBytesValue rbv => $"raw({rbv.Data.Length} bytes): {BitConverter.ToString(rbv.Data.Take(32).ToArray())}",
                                _ => p.Value.GetType().Name
                            };
                            lines.Add($"    0x{p.KeySymbol.Value:X16} = {valStr}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"  Parse error: {ex.GetType().Name}: {ex.Message}");
                }

                break; // Just the first one with choicestats
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Deep dive into the scene state blocks - parse the agent state format.
    /// After u32(block_size), the state contains serialized agent data.
    /// Telltale format: u32(agent_count) + per_agent(agent_name + props)
    /// </summary>
    [Fact]
    public void ParseSceneAgentStates()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

            var defData = ExtractDefaultSection(raw);
            if (defData == null) continue;

            lines.Add($"\n=== {season} ===");

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);
            var entryCount = br.ReadUInt32();

            for (uint i = 0; i < entryCount && ms.Position < ms.Length - 4; i++)
            {
                var strLen = br.ReadInt32();
                if (strLen <= 0 || strLen > 500 || ms.Position + strLen > ms.Length) break;
                var name = Encoding.ASCII.GetString(br.ReadBytes(strLen));

                if (ms.Position + 4 > ms.Length) break;
                var blockSize = br.ReadUInt32();
                if (blockSize > ms.Length - ms.Position) break;

                var stateStart = ms.Position;
                var stateData = br.ReadBytes((int)blockSize);

                // Only analyze a few entries per season
                if (i > 2) continue;

                lines.Add($"\n  [{i}] '{name}' ({blockSize} bytes)");

                if (blockSize < 4) continue;

                // Try to parse the state data
                using var stateMs = new MemoryStream(stateData);
                using var stateBr = new BinaryReader(stateMs);

                // First u32 - likely agent count
                var firstU32 = stateBr.ReadUInt32();
                lines.Add($"    First u32 (agent count?): {firstU32}");

                // Try interpreting each agent
                for (uint a = 0; a < Math.Min(firstU32, 5) && stateMs.Position < stateMs.Length - 4; a++)
                {
                    var agentStart = stateMs.Position;

                    // Try: u64(agent_symbol) + data
                    if (stateMs.Length - stateMs.Position >= 8)
                    {
                        var sym1 = stateBr.ReadUInt64();

                        // After symbol, what comes next?
                        if (stateMs.Length - stateMs.Position >= 32)
                        {
                            var next32 = new byte[Math.Min(32, (int)(stateMs.Length - stateMs.Position))];
                            stateMs.Read(next32, 0, next32.Length);
                            stateMs.Position -= next32.Length;
                            lines.Add($"    Agent {a}: sym=0x{sym1:X16}");
                            lines.Add($"      Next bytes: {BitConverter.ToString(next32)}");

                            // Try: u32(prop_count) after symbol
                            var propCount = BitConverter.ToUInt32(next32, 0);
                            lines.Add($"      Possible prop_count: {propCount}");

                            // Try: u32(agent_block_size) after symbol
                            var agentBlockSz = BitConverter.ToUInt32(next32, 0);
                            if (agentBlockSz > 0 && agentBlockSz < blockSize)
                            {
                                lines.Add($"      Possible agent_block_size: {agentBlockSz}");
                            }
                        }
                    }

                    // Reset and try different interpretation
                    stateMs.Position = agentStart;

                    // Try: each agent has a u32 size prefix
                    var agentSz = stateBr.ReadUInt32();
                    if (agentSz > 0 && agentSz < blockSize && agentSz + stateMs.Position <= stateMs.Length)
                    {
                        var agentData = stateBr.ReadBytes((int)agentSz);
                        var agentStrings = ExtractStrings(agentData, 4);
                        if (agentStrings.Count > 0)
                        {
                            lines.Add($"    Alt: Agent {a} (size-prefixed {agentSz} bytes): {agentStrings.Count} strings");
                            foreach (var s in agentStrings.Take(5))
                                lines.Add($"      \"{s}\"");
                        }
                        else
                        {
                            lines.Add($"    Alt: Agent {a} (size-prefixed {agentSz} bytes, no strings)");
                        }
                    }
                    else
                    {
                        stateMs.Position = agentStart + 8; // skip back to after first u64
                        lines.Add($"    Alt: Agent {a} size-prefix {agentSz} doesn't work");
                    }
                }

                // Also extract ALL strings from the entire state block
                var allStrings = ExtractStrings(stateData, 5);
                if (allStrings.Count > 0)
                {
                    lines.Add($"    All strings in block ({allStrings.Count}):");
                    foreach (var s in allStrings.Take(20))
                        lines.Add($"      \"{s}\"");
                    if (allStrings.Count > 20)
                        lines.Add($"      ... ({allStrings.Count - 20} more)");
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Extract all strings from ALL scene state blocks in one go,
    /// looking for anything that resembles game variables or choice flags.
    /// </summary>
    [Fact]
    public void ExtractAllSceneStrings()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

            var defData = ExtractDefaultSection(raw);
            if (defData == null) continue;

            // Extract ALL strings >= 4 chars from the entire default section
            var allStrings = ExtractStrings(defData, 4)
                .Where(s => s.All(c => c >= 32 && c < 127))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            lines.Add($"\n=== {season}: {allStrings.Count} unique strings from default.save ===");
            foreach (var s in allStrings)
                lines.Add($"  {s}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Try to decode state blocks by looking for Telltale's ChoreAgent serialization.
    /// Each agent might be: u64(name_crc) + u32(num_components) + per_component(...)
    /// Or: length-prefixed string name + serialized properties
    /// </summary>
    [Fact]
    public void AnalyzeFirstStateBlock()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

            var defData = ExtractDefaultSection(raw);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);
            var entryCount = br.ReadUInt32();

            // Parse first scene entry
            var strLen = br.ReadInt32();
            var name = Encoding.ASCII.GetString(br.ReadBytes(strLen));
            var blockSize = br.ReadUInt32();

            lines.Add($"Scene: '{name}', block size: {blockSize}");

            var stateData = br.ReadBytes((int)blockSize);

            // Full hex dump (first 512 bytes) with annotations
            lines.Add("\nFull hex dump (first 512 bytes):");
            for (int row = 0; row < Math.Min(512, stateData.Length); row += 16)
            {
                var chunk = stateData.Skip(row).Take(16).ToArray();
                var hex = string.Join(" ", chunk.Select(b => b.ToString("X2")));
                var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                lines.Add($"  {row:X4}: {hex,-48}  {ascii}");
            }

            // Try various interpretations of the first bytes
            lines.Add("\n--- Interpretations ---");

            using var sm = new MemoryStream(stateData);
            using var sbr = new BinaryReader(sm);

            // Read first few u32s
            for (int j = 0; j < Math.Min(16, stateData.Length / 4); j++)
            {
                sm.Position = j * 4;
                var val = sbr.ReadUInt32();
                lines.Add($"  u32[{j}] = {val} (0x{val:X8})");
            }

            // Read first few u64s
            for (int j = 0; j < Math.Min(8, stateData.Length / 8); j++)
            {
                sm.Position = j * 8;
                var val = sbr.ReadUInt64();
                lines.Add($"  u64[{j}] = 0x{val:X16}");
            }

            // Read first few floats
            for (int j = 0; j < Math.Min(16, stateData.Length / 4); j++)
            {
                sm.Position = j * 4;
                var val = BitConverter.ToSingle(stateData, j * 4);
                if (float.IsFinite(val) && Math.Abs(val) > 0.0001f && Math.Abs(val) < 100000f)
                    lines.Add($"  float[{j}] = {val:F4}");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static byte[]? ExtractDefaultSection(byte[] innerData)
    {
        try
        {
            using var ms = new MemoryStream(innerData);
            using var reader = new BinaryReaderEx(ms);
            reader.ReadUInt32(); // magic
            var defSize = reader.ReadUInt32();
            reader.ReadUInt32(); reader.ReadUInt32(); // dbg, async
            var verCount = reader.ReadUInt32();
            for (uint i = 0; i < verCount; i++) { reader.ReadUInt64(); reader.ReadUInt32(); }

            bool compressed = (defSize & 0x80000000) != 0;
            var rawSize = (int)(defSize & 0x7FFFFFFF);
            if (rawSize == 0 || reader.Remaining < rawSize) return null;
            var rawBytes = reader.ReadBytes(rawSize);

            if (!compressed) return rawBytes;
            if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
                return DecompressTtcz(rawBytes);

            using var cs = new MemoryStream(rawBytes);
            using var zlib = new ZLibStream(cs, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch { return null; }
    }

    private static byte[]? ExtractInnerDefaultSection(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReaderEx(ms);
            reader.ReadUInt32();
            var defSize = reader.ReadUInt32();
            reader.ReadUInt32(); reader.ReadUInt32();
            var verCount = reader.ReadUInt32();
            for (uint i = 0; i < verCount; i++) { reader.ReadUInt64(); reader.ReadUInt32(); }

            bool compressed = (defSize & 0x80000000) != 0;
            var rawSize = (int)(defSize & 0x7FFFFFFF);
            if (rawSize == 0 || reader.Remaining < rawSize) return null;
            var rawBytes = reader.ReadBytes(rawSize);

            if (!compressed) return rawBytes;
            if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A) return null;

            using var cs = new MemoryStream(rawBytes);
            using var zlib = new ZLibStream(cs, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch { return null; }
    }

    private static byte[] DecompressTtcz(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        br.ReadUInt32();
        var windowSize = br.ReadUInt32();
        var pageCount = br.ReadUInt32();
        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++) offsets[i] = br.ReadUInt64();

        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            ms.Position = (long)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            var block = br.ReadBytes(compLen);
            using var cs = new MemoryStream(block);
            using var deflate = new DeflateStream(cs, CompressionMode.Decompress);
            var buf = new byte[windowSize];
            int total = 0, n;
            while ((n = deflate.Read(buf, total, (int)windowSize - total)) > 0) total += n;
            output.Write(buf, 0, total);
        }
        return output.ToArray();
    }

    private static List<string> ExtractStrings(byte[] data, int minLength)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (b >= 32 && b < 127) sb.Append((char)b);
            else { if (sb.Length >= minLength) result.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length >= minLength) result.Add(sb.ToString());
        return result;
    }
}
