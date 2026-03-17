using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Part 4: Try multiple format hypotheses systematically.
/// </summary>
public class SceneFormatInvestigation4
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    /// <summary>
    /// Hypothesis A: u32(N) + u32(name_len) + name + N * (u32(size) + data[size])
    /// One scene name, then N size-prefixed blocks.
    /// </summary>
    [Fact]
    public void HypothesisA_OneNameThenNBlocks()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var count = br.ReadUInt32();
            var nameLen = br.ReadInt32();
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));

            lines.Add($"\n=== {season}: count={count}, scene='{name}' ===");

            int parsed = 0;
            for (uint i = 0; i < count && ms.Position + 4 <= ms.Length; i++)
            {
                var blockSize = br.ReadUInt32();
                if (blockSize > ms.Length - ms.Position)
                {
                    lines.Add($"  Block {i}: size={blockSize} exceeds remaining={ms.Length - ms.Position}");
                    break;
                }
                if (i < 5 || i == count - 1)
                    lines.Add($"  Block {i}: size={blockSize} @{ms.Position}");
                ms.Position += blockSize;
                parsed++;
            }

            lines.Add($"  Parsed {parsed}/{count} blocks, remaining={ms.Length - ms.Position}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Hypothesis B: u32(N) + N * (u32(name_len) + name + u64_hash_data)
    /// Scene names use CRC64 hashes stored as u64, not length-prefixed strings.
    /// First entry might be the "active" scene with a string name.
    /// </summary>
    [Fact]
    public void HypothesisB_SceneNamesAsCrc64()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var count = br.ReadUInt32();
            lines.Add($"count={count}");

            // Entry 0: string name
            var nameLen = br.ReadInt32();
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));
            lines.Add($"Entry 0: '{name}'");

            // Read the scene state for entry 0
            // Try: u32(state_size) + state_data
            var stateSize = br.ReadUInt32();
            lines.Add($"  stateSize={stateSize}");
            // Within state: u32(agent_count) + agents
            var agentCount = br.ReadUInt32();
            lines.Add($"  agentCount={agentCount}");
            // Skip rest of state
            ms.Position += stateSize - 4; // already read agentCount

            // Entry 1+: try u64(scene_hash) + u32(state_size) + state
            for (uint i = 1; i < Math.Min(count, 5) && ms.Position + 12 <= ms.Length; i++)
            {
                var sceneHash = br.ReadUInt64();
                var ss = br.ReadUInt32();
                lines.Add($"Entry {i}: hash=0x{sceneHash:X16}, stateSize={ss}");

                if (ss > ms.Length - ms.Position)
                {
                    lines.Add($"  OVERFLOW: size={ss} > remaining={ms.Length - ms.Position}");
                    break;
                }
                if (ss >= 4)
                {
                    var ac = br.ReadUInt32();
                    lines.Add($"  agentCount={ac}");
                    ms.Position += ss - 4;
                }
                else
                    ms.Position += ss;
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Hypothesis C: The first u32 (30) is NOT entry count.
    /// Maybe it's a version number, and the format is completely different.
    /// Try: u32(version) + u32(scene_name_len) + name + flat_binary_state
    /// </summary>
    [Fact]
    public void HypothesisC_VersionPlusState()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            lines.Add($"Total size: {defData.Length}");
            lines.Add($"First 8 u32s:");
            for (int i = 0; i < 8; i++)
                lines.Add($"  u32[{i}] = {BitConverter.ToUInt32(defData, i * 4)} (0x{BitConverter.ToUInt32(defData, i * 4):X8})");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Hypothesis D: u32(N) + u32(name_len) + name + u32(agent_count) + agents
    /// Where N is something else, and there's only ONE scene.
    /// Then each agent is: u64(hash) + u32(data_size) + data
    /// </summary>
    [Fact]
    public void HypothesisD_OneSceneWithAgents()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var firstU32 = br.ReadUInt32();
            var nameLen = br.ReadInt32();
            if (nameLen <= 0 || nameLen > 200) continue;
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));

            // After scene name, try: u32(agent_count) + agents
            // where each agent = u64(hash) + u32(data_size) + data
            var agentCount = br.ReadUInt32();
            lines.Add($"\n=== {season}: firstU32={firstU32}, scene='{name}', agentCount={agentCount} ===");

            // The "7" for S3 makes sense as agent count
            int parsed = 0;
            for (uint a = 0; a < agentCount && ms.Position + 12 <= ms.Length; a++)
            {
                var agentHash = br.ReadUInt64();
                var agentDataSize = br.ReadUInt32();

                if (agentDataSize > ms.Length - ms.Position)
                {
                    lines.Add($"  Agent {a}: hash=0x{agentHash:X16}, size={agentDataSize} OVERFLOW (remaining={ms.Length - ms.Position})");
                    break;
                }

                if (a < 10 || a == agentCount - 1)
                    lines.Add($"  Agent {a}: hash=0x{agentHash:X16}, size={agentDataSize}");

                ms.Position += agentDataSize;
                parsed++;
            }

            var remaining = ms.Length - ms.Position;
            lines.Add($"  Parsed {parsed}/{agentCount} agents, remaining={remaining}");

            if (remaining > 0 && remaining < 20)
            {
                var tail = new byte[remaining];
                ms.Read(tail, 0, (int)remaining);
                lines.Add($"  Tail: {BitConverter.ToString(tail)}");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Hypothesis E: Format is a series of scenes, each starting with u64(hash) except first.
    /// u32(count) + scene_0(u32 name_len + name) + scene_0_state +
    /// scene_1(u64 hash) + scene_1_state + ...
    /// Where state = u32(agent_count) + agents, agent = u64(hash) + serialized_data
    /// And serialized_data has NO size prefix — we must parse it.
    /// </summary>
    [Fact]
    public void HypothesisE_FullParse()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var count = br.ReadUInt32();
            lines.Add($"count={count}");

            // Scene 0: string name
            var nameLen = br.ReadInt32();
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));
            lines.Add($"Scene 0: '{name}'");

            // Try: agent_count + per_agent(u64 hash + variable data)
            var agentCount = br.ReadUInt32();
            lines.Add($"  agentCount={agentCount}");

            // For each agent, try to parse:
            // u64(hash) + props until we hit something that looks like next agent's hash
            for (uint a = 0; a < agentCount && ms.Position + 8 <= ms.Length; a++)
            {
                var pos = ms.Position;
                var agentHash = br.ReadUInt64();

                // Read ahead to see what's there
                var peek = new byte[Math.Min(64, (int)(ms.Length - ms.Position))];
                ms.Read(peek, 0, peek.Length);
                ms.Position = pos + 8;

                lines.Add($"\n  Agent {a}: hash=0x{agentHash:X16} @{pos}");
                lines.Add($"    peek: {BitConverter.ToString(peek.Take(32).ToArray())}");

                // Try: u32(num_props) then per prop: u64(key_hash) + typed_value
                var numProps = BitConverter.ToUInt32(peek, 0);
                lines.Add($"    H: numProps={numProps}");

                // Each prop might be: u64(key) + u32(type_or_size) + value
                // Or: u64(key) + value (type determined by key)

                // For small numProps, try to read each
                if (numProps > 0 && numProps < 100)
                {
                    ms.Position = pos + 8 + 4; // skip hash + numProps
                    bool valid = true;
                    for (uint p = 0; p < Math.Min(numProps, 5) && ms.Position + 8 <= ms.Length; p++)
                    {
                        var propKey = br.ReadUInt64();
                        if (ms.Position >= ms.Length) { valid = false; break; }

                        // What follows the key?
                        var nextByte = br.ReadByte();
                        ms.Position--;

                        var nextU32 = ms.Position + 4 <= ms.Length ? BitConverter.ToUInt32(defData, (int)ms.Position) : 0u;

                        lines.Add($"      Prop {p}: key=0x{propKey:X16}, nextByte=0x{nextByte:X2}, nextU32={nextU32}");
                    }

                    // We don't know how to skip the rest, so just move on
                    ms.Position = pos + 8; // reset to after agent hash
                }

                // Try alternative: u32(total_agent_size) after hash
                var agentSize = BitConverter.ToUInt32(peek, 0);
                lines.Add($"    Alt: agentSize={agentSize}");
                if (agentSize > 0 && agentSize < defData.Length && pos + 8 + 4 + agentSize <= ms.Length)
                {
                    lines.Add($"      -> skip to {pos + 8 + 4 + agentSize}");
                }

                // Just skip to a reasonable position for now
                break; // Only analyze first agent deeply
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Let's try HypothesisD more carefully:
    /// Maybe the firstU32 (30/41/48) = scene_count NOT agent_count,
    /// and the scene name + u32(435/8182/4461) is the first scene's data including
    /// embedded scene name, and we should parse scene_count scenes where each has
    /// NO header — just consecutive u32-size-prefixed blocks.
    /// i.e., after the scene name, the rest is: count × u32(block_size) + data
    /// </summary>
    [Fact]
    public void HypothesisF_ConsecutiveSizePrefixedBlocks()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var count = br.ReadUInt32();
            var nameLen = br.ReadInt32();
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));

            lines.Add($"\n=== {season}: count={count}, scene='{name}' ===");

            // After scene name: consecutive u32-size-prefixed blocks
            int parsed = 0;
            var sizes = new List<uint>();
            while (ms.Position + 4 <= ms.Length)
            {
                var blockSize = br.ReadUInt32();
                if (blockSize == 0 || blockSize > ms.Length - ms.Position)
                {
                    lines.Add($"  Block {parsed}: size={blockSize} — stopping (remaining={ms.Length - ms.Position + 4})");
                    ms.Position -= 4; // back up
                    break;
                }
                sizes.Add(blockSize);
                ms.Position += blockSize;
                parsed++;
                if (parsed > 200) break; // safety
            }

            lines.Add($"  Parsed {parsed} blocks, remaining={ms.Length - ms.Position}");
            lines.Add($"  Block sizes: {string.Join(", ", sizes.Take(20))}");
            if (sizes.Count > 20) lines.Add($"  ... ({sizes.Count - 20} more)");
            lines.Add($"  Total block data: {sizes.Sum(s => s + 4)}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Ultimate test: try parsing with the TelltaleToolLib ChoreAgent format.
    /// SceneSave = u32(scene_count) + per_scene(u32(name_len) + name + u32(agent_count) + per_agent)
    /// per_agent = u64(name_hash) + u32(runtime_prop_count) + per_prop(u64(key) + serialized_value)
    /// serialized_value = type-dependent, but the key hash determines the type.
    /// Since we don't have the type info, try: each property is u64(key) + u32(len) + data[len]
    /// </summary>
    [Fact]
    public void ParseWithAgentPropertyFormat()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3" })
        {
            var defData = GetDefaultSave(season);
            if (defData == null) continue;

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var sceneCount = br.ReadUInt32(); // 30
            lines.Add($"sceneCount={sceneCount}");

            for (uint s = 0; s < sceneCount && ms.Position + 4 <= ms.Length; s++)
            {
                // Scene name: u32(len) + string
                var nameLen = br.ReadInt32();
                if (nameLen <= 0 || nameLen > 200 || ms.Position + nameLen > ms.Length)
                {
                    lines.Add($"Scene {s}: invalid nameLen={nameLen} @{ms.Position - 4}");

                    // Maybe scene names after first use u64 hashes?
                    ms.Position -= 4; // back up
                    if (ms.Position + 8 <= ms.Length)
                    {
                        var sceneHash = br.ReadUInt64();
                        lines.Add($"  Trying u64 hash: 0x{sceneHash:X16}");

                        // After hash, try u32(agent_count)
                        if (ms.Position + 4 <= ms.Length)
                        {
                            var ac = br.ReadUInt32();
                            lines.Add($"  agentCount={ac}");

                            if (ac > 0 && ac < 500)
                            {
                                // Try to parse agents
                                for (uint a = 0; a < ac && ms.Position + 8 <= ms.Length; a++)
                                {
                                    var agentHash = br.ReadUInt64();
                                    // Try reading props
                                    if (ms.Position + 4 > ms.Length) break;
                                    var propCount = br.ReadUInt32();
                                    if (propCount > 1000)
                                    {
                                        lines.Add($"    Agent {a}: hash=0x{agentHash:X16}, propCount={propCount} — too many");
                                        ms.Position -= 4;
                                        break;
                                    }
                                    if (a < 3)
                                        lines.Add($"    Agent {a}: hash=0x{agentHash:X16}, propCount={propCount}");

                                    // Skip props (u64 key + u32 value_size + data for each)
                                    for (uint p = 0; p < propCount; p++)
                                    {
                                        if (ms.Position + 12 > ms.Length) break;
                                        var key = br.ReadUInt64();
                                        var valSize = br.ReadUInt32();
                                        if (valSize > ms.Length - ms.Position)
                                        {
                                            if (a < 3) lines.Add($"      Prop {p}: key=0x{key:X16}, valSize={valSize} OVERFLOW");
                                            ms.Position -= 12; // back up
                                            break;
                                        }
                                        if (a < 3 && p < 3)
                                            lines.Add($"      Prop {p}: key=0x{key:X16}, valSize={valSize}");
                                        ms.Position += valSize;
                                    }
                                }
                            }
                        }
                    }
                    break;
                }

                var sceneName = Encoding.ASCII.GetString(br.ReadBytes(nameLen));
                lines.Add($"\nScene {s}: '{sceneName}'");

                // Agent count
                if (ms.Position + 4 > ms.Length) break;
                var agentCountLocal = br.ReadUInt32();
                lines.Add($"  agentCount={agentCountLocal}");

                // Parse agents
                for (uint a = 0; a < agentCountLocal && ms.Position + 8 <= ms.Length; a++)
                {
                    var agentHash = br.ReadUInt64();
                    if (ms.Position + 4 > ms.Length) break;
                    var propCount = br.ReadUInt32();

                    if (propCount > 1000)
                    {
                        lines.Add($"    Agent {a}: hash=0x{agentHash:X16}, propCount={propCount} — too many, likely wrong format");
                        ms.Position -= 4;
                        break;
                    }

                    if (a < 5 || a == agentCountLocal - 1)
                        lines.Add($"    Agent {a}: hash=0x{agentHash:X16}, propCount={propCount}");

                    // Each prop: u64(key) + type-dependent value
                    // Since we don't know types, try u64(key) + u32(size) + data
                    for (uint p = 0; p < propCount; p++)
                    {
                        if (ms.Position + 12 > ms.Length) break;
                        var key = br.ReadUInt64();
                        var valSize = br.ReadUInt32();
                        if (valSize > ms.Length - ms.Position || valSize > 100000)
                        {
                            if (a < 5) lines.Add($"      Prop {p}: key=0x{key:X16}, valSize={valSize} — overflow or wrong");
                            ms.Position -= 12;
                            break;
                        }
                        ms.Position += valSize;
                    }
                }
            }

            lines.Add($"\nFinal position: {ms.Position}/{ms.Length} (remaining={ms.Length - ms.Position})");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private byte[]? GetDefaultSave(string season)
    {
        var dir = Path.Combine(SaveDir, season);
        if (!Directory.Exists(dir)) return null;
        var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
        if (file == null) return null;
        var slot = BundleReader.Read(file);
        if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) return null;
        return ExtractDefaultSection(raw);
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
