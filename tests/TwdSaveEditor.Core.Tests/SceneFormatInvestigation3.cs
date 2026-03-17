using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Part 3: Pin down the exact scene save format by scanning for .lua boundaries.
/// </summary>
public class SceneFormatInvestigation3
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    /// <summary>
    /// Find all offsets of length-prefixed strings ending in .lua in the default section.
    /// This tells us where each scene entry starts and the exact gap between them.
    /// </summary>
    [Fact]
    public void FindLuaStringBoundaries()
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

            var entryCount = BitConverter.ToUInt32(defData, 0);
            lines.Add($"\n=== {season}: {defData.Length} bytes, entry_count={entryCount} ===");

            // Scan for all occurrences of ".lua" in the data
            var luaOffsets = new List<(int nameStart, int nameLen, string name)>();
            for (int i = 4; i < defData.Length - 8; i++)
            {
                // Check for length-prefixed string ending in .lua
                var possibleLen = BitConverter.ToInt32(defData, i);
                if (possibleLen > 4 && possibleLen < 200 && i + 4 + possibleLen <= defData.Length)
                {
                    var endOffset = i + 4 + possibleLen;
                    // Check if the string ends with .lua
                    if (endOffset >= 4 &&
                        defData[endOffset - 4] == '.' &&
                        defData[endOffset - 3] == 'l' &&
                        defData[endOffset - 2] == 'u' &&
                        defData[endOffset - 1] == 'a')
                    {
                        var str = Encoding.ASCII.GetString(defData, i + 4, possibleLen);
                        if (str.All(c => c >= 32 && c < 127))
                        {
                            luaOffsets.Add((i, possibleLen, str));
                        }
                    }
                }
            }

            lines.Add($"  Found {luaOffsets.Count} .lua strings (expected {entryCount})");

            // Show each .lua name and the gap between them
            for (int j = 0; j < luaOffsets.Count; j++)
            {
                var (nameStart, nameLen, name) = luaOffsets[j];
                var dataStart = nameStart + 4 + nameLen; // offset of data after the name

                int gapToNext = j + 1 < luaOffsets.Count
                    ? luaOffsets[j + 1].nameStart - dataStart
                    : defData.Length - dataStart;

                lines.Add($"  [{j}] @{nameStart}: '{name}' (dataStart={dataStart}, stateSize={gapToNext})");

                // Dump first 32 bytes of state data
                if (gapToNext > 0)
                {
                    var statePreview = new byte[Math.Min(32, gapToNext)];
                    Array.Copy(defData, dataStart, statePreview, 0, statePreview.Length);
                    lines.Add($"       state: {BitConverter.ToString(statePreview)}");

                    // First few u32s
                    if (gapToNext >= 4)
                    {
                        var u32_0 = BitConverter.ToUInt32(defData, dataStart);
                        lines.Add($"       u32[0]={u32_0}");
                    }
                    if (gapToNext >= 8)
                    {
                        var u32_1 = BitConverter.ToUInt32(defData, dataStart + 4);
                        lines.Add($"       u32[1]={u32_1}");
                    }
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Now that we know boundaries, parse entry by entry:
    /// After the scene name, read the first u32 (agent_count?) and try to
    /// parse each agent as u64(name_hash) + per-agent data.
    /// </summary>
    [Fact]
    public void ParseAgentsInFirstScene()
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

            // Find the first two .lua boundaries to know the exact state data extent
            var luaOffsets = FindLuaStrings(defData);
            if (luaOffsets.Count < 2) continue;

            var (nameStart, nameLen, name) = luaOffsets[0];
            var dataStart = nameStart + 4 + nameLen;
            var dataEnd = luaOffsets[1].nameStart;
            var stateLen = dataEnd - dataStart;

            lines.Add($"Scene: '{name}', state: {stateLen} bytes (offset {dataStart} to {dataEnd})");

            // Full hex dump of the state block
            var stateData = new byte[stateLen];
            Array.Copy(defData, dataStart, stateData, 0, stateLen);

            lines.Add("\nFull state hex dump:");
            for (int row = 0; row < stateData.Length; row += 16)
            {
                var chunk = stateData.Skip(row).Take(16).ToArray();
                var hex = string.Join(" ", chunk.Select(b => b.ToString("X2")));
                var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                lines.Add($"  {row:X4}: {hex,-48}  {ascii}");
            }

            // Interpret the data with various hypotheses
            using var ms = new MemoryStream(stateData);
            using var br = new BinaryReader(ms);

            lines.Add($"\n--- Hypothesis: u32(agent_count) + per_agent(u64 hash + u32 prop_count + props) ---");
            var agentCount = br.ReadUInt32();
            lines.Add($"agent_count = {agentCount}");

            for (uint a = 0; a < agentCount && ms.Position < ms.Length - 8; a++)
            {
                var agentHash = br.ReadUInt64();
                lines.Add($"\n  Agent {a}: hash=0x{agentHash:X16} (@{ms.Position - 8})");

                if (ms.Position >= ms.Length) break;

                // What follows the agent hash?
                var nextBytes = new byte[Math.Min(48, (int)(ms.Length - ms.Position))];
                var savedPos = ms.Position;
                ms.Read(nextBytes, 0, nextBytes.Length);
                ms.Position = savedPos;

                lines.Add($"    next bytes: {BitConverter.ToString(nextBytes)}");

                // Try: u32(prop_count) + per_prop(...)
                var propCount = BitConverter.ToUInt32(nextBytes, 0);
                lines.Add($"    H: prop_count = {propCount}");

                // Try: there's some fixed-size per-agent data
                // Look for patterns: all agents same size?
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Try to figure out the per-agent data size by comparing multiple scenes.
    /// If agents have variable-size data, we need to parse property by property.
    /// </summary>
    [Fact]
    public void CompareSceneStateSizes()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Use MULTIPLE checkpoint files to compare
            var files = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .Take(3)
                .ToList();

            foreach (var file in files)
            {
                var slot = BundleReader.Read(file);
                if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

                var defData = ExtractDefaultSection(raw);
                if (defData == null) continue;

                var luaOffsets = FindLuaStrings(defData);
                var entryCount = BitConverter.ToUInt32(defData, 0);

                lines.Add($"\n{season}/{Path.GetFileName(file)}: {entryCount} entries, found {luaOffsets.Count} .lua names");

                for (int j = 0; j < luaOffsets.Count; j++)
                {
                    var (nameStart, nameLen, name) = luaOffsets[j];
                    var dataStart = nameStart + 4 + nameLen;
                    var dataEnd = j + 1 < luaOffsets.Count ? luaOffsets[j + 1].nameStart : defData.Length;
                    var stateLen = dataEnd - dataStart;

                    // First u32 in state data
                    var firstU32 = stateLen >= 4 ? BitConverter.ToUInt32(defData, dataStart) : 0u;

                    lines.Add($"  [{j}] '{name}' state={stateLen}b first_u32={firstU32}");
                }

                break; // Just first file per season
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Extended CRC64 brute-force using patterns from TelltaleToolLib/community sources.
    /// Try MANY more naming conventions.
    /// </summary>
    [Fact]
    public void ExtendedCrc64BruteForce()
    {
        var lines = new List<string>();

        // Collect all target hashes
        var targetHashes = new HashSet<ulong>();
        foreach (var season in new[] { "S3", "S4", "Michonne" })
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
                        targetHashes.Add(p.KeySymbol.Value);
            }
        }

        lines.Add($"Target hashes: {targetHashes.Count}");

        // Also add known matched hashes for context
        // From S1/S2 we already know some:
        var knownHashes = new Dictionary<ulong, string>();

        // First, verify our known S1 metadata hash mappings
        var metadataKeys = new[]
        {
            "playtime", "mPlaytime", "mTotalPlaytime", "Playtime", "TotalPlaytime",
            "autosave_file", "mAutosaveFile", "AutosaveFile",
            "episode", "mEpisode", "Episode", "CurrentEpisode",
            "slot", "mSlot", "Slot", "SaveSlot",
            "mMaxEpisode", "MaxEpisode", "max_episode", "maxEpisode",
            "mMinEpisode", "MinEpisode", "min_episode", "minEpisode",
            "mCurrentEpisode", "current_episode", "currentEpisode",
            // The known non-bool keys from S3/S4 metadata:
            // 0x7C725227A47FD1BA = int (21, 37, 69, 89, 118, 119, 135, 161, 178) — looks like playtime
            // 0xB218E7C003A67CE9 = int (1, 2, 3, 4, 5) — looks like episode number
            // 0xFD50E3BE7B29A8B1 = int (1, 2, 3, 4) — looks like max episode or something
            // 0xF235E9FCE9562E01 = string "_wd3_saveslot1_autosave.bundle" — autosave filename
            // 0x0399C2FFE0D50348 = int (1, 2) — some counter
        };

        // Known int/string key hashes from the slot data
        var intKeyTargets = new HashSet<ulong>
        {
            0x7C725227A47FD1BA, // int: 21-178 (playtime?)
            0xB218E7C003A67CE9, // int: 1-5 (episode number)
            0xFD50E3BE7B29A8B1, // int: 1-4
            0x0399C2FFE0D50348, // int: 1-2
        };
        var stringKeyTargets = new HashSet<ulong>
        {
            0xF235E9FCE9562E01, // string: autosave filename
        };

        var allTargets = targetHashes
            .Union(intKeyTargets)
            .Union(stringKeyTargets)
            .ToHashSet();

        lines.Add($"All targets (incl int/string): {allTargets.Count}");

        // MASSIVE candidate list
        var candidates = new HashSet<string>();

        // Common Telltale property names from various sources
        var patterns = new[]
        {
            // From TelltaleToolLib known property names
            "mPlaytimeSeconds", "mPlaytime", "mTotalPlaytime", "mTotalPlaytimeSeconds",
            "mCurrentEpisode", "mMaxEpisode", "mMinEpisode",
            "mEpisode", "mSlot", "mSlotNum", "mSaveSlot",
            "mAutosaveFile", "mAutosaveFilename", "mAutosavePath",
            "mCheckpointFile", "mCheckpoint",
            "mLastPlayedEpisode", "mHighestPlayedEpisode",
            "mCompletedEpisodes", "mCompletedEpisodeCount",
            "mEpisodeCompletion", "mIsComplete", "mHasCompleted",
            "mChoices", "mMadeChoices", "mHasChoices",
            "mFlags", "mGameFlags", "mStoryFlags",

            // Common Telltale naming: "classname.propertyname" (Symbol from full qualified name)
            "SaveSlotInfo.mPlaytimeSeconds",
            "SaveSlotInfo.mCurrentEpisode",
            "SaveSlotInfo.mMaxEpisode",
            "SaveSlotInfo.mAutosaveFile",

            // Episode tracking
            "Episode1Complete", "Episode2Complete", "Episode3Complete",
            "Episode4Complete", "Episode5Complete",
            "episode1complete", "episode2complete", "episode3complete",
            "episode4complete", "episode5complete",
            "ep1complete", "ep2complete", "ep3complete",
            "ep4complete", "ep5complete",
            "bEpisode1Complete", "bEpisode2Complete", "bEpisode3Complete",
            "bEpisode4Complete", "bEpisode5Complete",
            "EpisodeCompleted1", "EpisodeCompleted2", "EpisodeCompleted3",
            "EpisodeCompleted4", "EpisodeCompleted5",
            "mEpisode1Complete", "mEpisode2Complete", "mEpisode3Complete",
            "mEpisode4Complete", "mEpisode5Complete",

            // S4 specific
            "BoardingSchool", "Ericson", "EricsonsSchool",
            "StableEnding", "BadEnding",
            "SavedLouis", "SavedViolet", "LouisSaved", "VioletSaved",
            "AJTrusted", "AJDisTrusted",

            // Import tracking (Definitive Series imports choices between seasons)
            "mHasImport", "mImportData", "bHasImport",
            "bImported", "mImported",
            "Season1Import", "Season2Import", "Season3Import",
            "mSeason1Import", "mSeason2Import", "mSeason3Import",
            "bHasSeason1Import", "bHasSeason2Import", "bHasSeason3Import",
            "s1import", "s2import", "s3import",

            // Telemetry/stats
            "mChoiceStats", "mStats",
            "mStatsCount", "mChoiceCount",
        };

        foreach (var p in patterns) candidates.Add(p);

        // Generate systematic variations
        for (int ep = 1; ep <= 5; ep++)
        {
            candidates.Add($"Episode {ep} Complete");
            candidates.Add($"Episode{ep}Complete");
            candidates.Add($"mEpisode{ep}Complete");
            candidates.Add($"bEpisode{ep}Complete");
            candidates.Add($"episode_{ep}_complete");
            candidates.Add($"ep{ep}_complete");
            candidates.Add($"ep_{ep}_complete");
            candidates.Add($"EpComplete{ep}");
            candidates.Add($"mEpComplete{ep}");
            candidates.Add($"bEpComplete{ep}");
            candidates.Add($"complete_ep{ep}");
            candidates.Add($"complete_episode{ep}");
            candidates.Add($"completedEpisode{ep}");
            candidates.Add($"has_completed_ep{ep}");
            candidates.Add($"hasCompletedEp{ep}");
            candidates.Add($"mHasCompletedEp{ep}");
        }

        // Season-prefixed variations
        foreach (var prefix in new[] { "wd3_", "wd4_", "wdm_", "s3_", "s4_", "michonne_", "" })
        {
            for (int ep = 1; ep <= 5; ep++)
            {
                candidates.Add($"{prefix}episode_{ep}_complete");
                candidates.Add($"{prefix}ep{ep}_complete");
                candidates.Add($"{prefix}ep{ep}complete");
            }
            candidates.Add($"{prefix}playtime");
            candidates.Add($"{prefix}autosave");
            candidates.Add($"{prefix}episode");
            candidates.Add($"{prefix}slot");
        }

        // From known property databases - try common Telltale type hashes
        // These are property names from the Definitive Series
        var defSeriesNames = new[]
        {
            "mPlaytime_seconds", "mPlaytime_Seconds", "mPlaytimeSecs",
            "mTotalPlaytime_seconds", "mTotalPlaytime_Seconds",
            "Playtime Seconds", "PlaytimeSeconds",
            "Current Episode", "CurrentEpisode", "Max Episode", "MaxEpisode",
            "Autosave File", "AutosaveFile",
            "Min Episode", "MinEpisode",
            "Episode Count", "EpisodeCount",
            "Has Played Episode", "HasPlayedEpisode",
            "HasEverPlayed",
            "mPlaytimeInSeconds",
            "mTotalTimePlayed",
            "mTimePlayed",
            "PlayTime", "playTime",
            "saveSlotIndex", "SaveSlotIndex", "mSaveSlotIndex",
            "currentEpisodeIndex", "CurrentEpisodeIndex", "mCurrentEpisodeIndex",
            "maxEpisodeIndex", "MaxEpisodeIndex", "mMaxEpisodeIndex",
        };
        foreach (var n in defSeriesNames) candidates.Add(n);

        // Compute all hashes
        var hashToName = new Dictionary<ulong, string>();
        foreach (var c in candidates)
        {
            var hash = TelltaleHash.ComputeCrc64(c);
            if (allTargets.Contains(hash))
                hashToName[hash] = c;
        }

        lines.Add($"\nTried {candidates.Count} candidates");
        lines.Add($"Matches found: {hashToName.Count}");

        foreach (var (hash, name) in hashToName.OrderBy(kv => kv.Key))
            lines.Add($"  0x{hash:X16} = '{name}'");

        lines.Add("\nUnmatched hashes:");
        foreach (var h in allTargets.Where(h => !hashToName.ContainsKey(h)).OrderBy(h => h))
        {
            var category = intKeyTargets.Contains(h) ? " [int]" :
                           stringKeyTargets.Contains(h) ? " [string]" :
                           targetHashes.Contains(h) ? " [bool]" : "";
            lines.Add($"  0x{h:X16}{category}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static List<(int nameStart, int nameLen, string name)> FindLuaStrings(byte[] data)
    {
        var result = new List<(int, int, string)>();
        for (int i = 4; i < data.Length - 8; i++)
        {
            var len = BitConverter.ToInt32(data, i);
            if (len > 4 && len < 200 && i + 4 + len <= data.Length)
            {
                var end = i + 4 + len;
                if (data[end - 4] == '.' && data[end - 3] == 'l' && data[end - 2] == 'u' && data[end - 1] == 'a')
                {
                    var str = Encoding.ASCII.GetString(data, i + 4, len);
                    if (str.All(c => c >= 32 && c < 127))
                        result.Add((i, len, str));
                }
            }
        }
        return result;
    }

    private static byte[]? ExtractDefaultSection(byte[] innerData)
    {
        try
        {
            using var ms = new MemoryStream(innerData);
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
            if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
                return DecompressTtcz(rawBytes);
            using var cs = new MemoryStream(rawBytes);
            using var zlib = new System.IO.Compression.ZLibStream(cs, System.IO.Compression.CompressionMode.Decompress);
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
        var ws = br.ReadUInt32();
        var pc = br.ReadUInt32();
        var off = new ulong[pc + 1];
        for (int i = 0; i <= pc; i++) off[i] = br.ReadUInt64();
        using var output = new MemoryStream();
        for (int i = 0; i < pc; i++)
        {
            ms.Position = (long)off[i];
            var block = br.ReadBytes((int)(off[i + 1] - off[i]));
            using var cs = new MemoryStream(block);
            using var deflate = new System.IO.Compression.DeflateStream(cs, System.IO.Compression.CompressionMode.Decompress);
            var buf = new byte[ws];
            int t = 0, n;
            while ((n = deflate.Read(buf, t, (int)ws - t)) > 0) t += n;
            output.Write(buf, 0, t);
        }
        return output.ToArray();
    }
}
