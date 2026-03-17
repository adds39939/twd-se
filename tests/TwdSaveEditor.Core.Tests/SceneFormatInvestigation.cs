using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

/// <summary>
/// Deep investigation of the default.save scene format used by S3/S4/Michonne checkpoint files.
/// These files do NOT use ChoicesContainer — we need to understand what format they DO use.
/// </summary>
public class SceneFormatInvestigation
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    /// <summary>
    /// Parse the default.save inner file from each season's largest checkpoint bundle.
    /// Dumps the full structure: entry count, Lua scene filenames, binary scene state sizes.
    /// </summary>
    [Fact]
    public void ParseDefaultSaveSceneEntries()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S1", "S2", "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Try both slot files and checkpoint files
            var allFiles = Directory.GetFiles(dir, "*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .Take(3)
                .ToList();

            foreach (var file in allFiles)
            {
                var slot = BundleReader.Read(file);
                if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

                lines.Add($"\n=== {season}: {Path.GetFileName(file)} ({raw.Length} bytes) ===");

                // Extract default section from inner MetaStream
                using var ms = new MemoryStream(raw);
                using var reader = new BinaryReaderEx(ms);
                var magic = reader.ReadUInt32();
                var defSize = reader.ReadUInt32();
                var dbgSize = reader.ReadUInt32();
                var asyncSize = reader.ReadUInt32();
                var verCount = reader.ReadUInt32();
                lines.Add($"  Header: magic=0x{magic:X8} def=0x{defSize:X8} dbg=0x{dbgSize:X8} async=0x{asyncSize:X8} vers={verCount}");

                for (uint i = 0; i < verCount; i++) { reader.ReadUInt64(); reader.ReadUInt32(); }

                bool compressed = (defSize & 0x80000000) != 0;
                var rawSize = (int)(defSize & 0x7FFFFFFF);
                if (rawSize == 0 || reader.Remaining < rawSize) { lines.Add("  Empty/insufficient default section"); continue; }
                var rawBytes = reader.ReadBytes(rawSize);

                byte[] defData;
                if (compressed)
                {
                    if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
                    {
                        lines.Add($"  Default section is TTCZ compressed ({rawSize} bytes)");
                        // Use BundleReader's decompression via reflection or re-implement
                        try
                        {
                            defData = DecompressTtcz(rawBytes);
                            lines.Add($"  Decompressed to {defData.Length} bytes");
                        }
                        catch (Exception ex)
                        {
                            lines.Add($"  TTCZ decompression failed: {ex.Message}");
                            continue;
                        }
                    }
                    else
                    {
                        using var cs = new MemoryStream(rawBytes);
                        using var zlib = new ZLibStream(cs, CompressionMode.Decompress);
                        using var output = new MemoryStream();
                        zlib.CopyTo(output);
                        defData = output.ToArray();
                        lines.Add($"  Zlib decompressed to {defData.Length} bytes");
                    }
                }
                else
                {
                    defData = rawBytes;
                    lines.Add($"  Default section uncompressed: {defData.Length} bytes");
                }

                // Now parse the scene save format
                ParseSceneSaveData(defData, lines);

                // Also check debug section for symbol names
                var dbgSz = (int)(dbgSize & 0x7FFFFFFF);
                if (dbgSz > 0 && reader.Remaining >= dbgSz)
                {
                    var dbgData = reader.ReadBytes(dbgSz);
                    var nonZero = dbgData.Count(b => b != 0);
                    lines.Add($"  Debug section: {dbgSz} bytes ({nonZero} non-zero)");
                }

                // Check async section too
                var asyncSz = (int)(asyncSize & 0x7FFFFFFF);
                if (asyncSz > 0 && reader.Remaining >= asyncSz)
                {
                    var asyncData = reader.ReadBytes(asyncSz);
                    lines.Add($"  Async section: {asyncSz} bytes");
                    lines.Add($"  Async first 64: {BitConverter.ToString(asyncData.Take(64).ToArray())}");
                }

                break; // Just the first/largest file per season
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Try to interpret the default.save default section data.
    /// Known format: u32(entry_count) + per_entry(u32(str_len) + lua_scene_filename + binary_scene_state)
    /// We need to figure out where each entry ends.
    /// </summary>
    private void ParseSceneSaveData(byte[] data, List<string> lines)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        // Try reading as entry_count + entries
        var first4 = br.ReadUInt32();
        lines.Add($"  First u32: {first4} (0x{first4:X8})");

        // Could be entry count, or could be something else
        // Try interpreting as entry count
        if (first4 > 0 && first4 < 10000)
        {
            lines.Add($"  Interpreting as {first4} scene entries...");
            int entriesParsed = 0;
            var sceneNames = new List<string>();

            for (uint i = 0; i < first4 && ms.Position < ms.Length; i++)
            {
                var entryStart = ms.Position;

                if (ms.Length - ms.Position < 4) break;
                var strLen = br.ReadInt32();

                // Validate string length
                if (strLen <= 0 || strLen > 500 || ms.Position + strLen > ms.Length)
                {
                    lines.Add($"  Entry {i}: invalid strLen={strLen} at offset {entryStart}");
                    // Dump surrounding bytes for context
                    ms.Position = entryStart;
                    var context = br.ReadBytes((int)Math.Min(64, ms.Length - ms.Position));
                    lines.Add($"    Context: {BitConverter.ToString(context)}");
                    break;
                }

                var nameBytes = br.ReadBytes(strLen);
                var name = Encoding.ASCII.GetString(nameBytes);
                sceneNames.Add(name);
                entriesParsed++;

                // Now we need to figure out how much binary data follows
                // Try to detect the pattern: what comes after the scene name?
                var afterNamePos = ms.Position;
                if (ms.Length - ms.Position < 4)
                {
                    lines.Add($"  Entry {i}: '{name}' — no more data after name");
                    break;
                }

                // Read ahead to understand the binary scene state format
                if (i < 10) // Only detail first 10
                {
                    var nextBytes = new byte[Math.Min(64, (int)(ms.Length - ms.Position))];
                    ms.Read(nextBytes, 0, nextBytes.Length);
                    ms.Position = afterNamePos; // restore

                    lines.Add($"  Entry {i}: '{name}' at offset {entryStart}");
                    lines.Add($"    After name ({afterNamePos}): {BitConverter.ToString(nextBytes.Take(32).ToArray())}");

                    // Try to interpret the binary state
                    // Hypothesis 1: u32 size prefix for remaining data
                    var possibleSize = BitConverter.ToUInt32(nextBytes, 0);
                    lines.Add($"    Next u32 as size: {possibleSize}");

                    // Hypothesis 2: The state is a serialized block with known length
                    // Try scanning for the next valid scene name start
                }
            }

            lines.Add($"  Parsed {entriesParsed} scene entries");
            if (sceneNames.Count > 10)
            {
                foreach (var name in sceneNames.Take(10))
                    lines.Add($"    {name}");
                lines.Add($"    ... ({sceneNames.Count - 10} more)");
                foreach (var name in sceneNames.TakeLast(3))
                    lines.Add($"    {name}");
            }
            else
            {
                foreach (var name in sceneNames)
                    lines.Add($"    {name}");
            }
        }
        else
        {
            lines.Add("  Does NOT look like entry count, dumping raw header:");
            ms.Position = 0;
            var header = br.ReadBytes((int)Math.Min(256, ms.Length));
            for (int row = 0; row < header.Length; row += 32)
            {
                var chunk = header.Skip(row).Take(32).ToArray();
                var hex = BitConverter.ToString(chunk);
                var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                lines.Add($"    {row:X4}: {hex}  {ascii}");
            }
        }
    }

    /// <summary>
    /// Try multiple hypotheses for how scene state data is delimited after each scene name.
    /// </summary>
    [Fact]
    public void AnalyzeSceneStateDelimitation()
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

            lines.Add($"\n=== {season}: {Path.GetFileName(file)} (default section: {defData.Length} bytes) ===");

            using var ms = new MemoryStream(defData);
            using var br = new BinaryReader(ms);

            var entryCount = br.ReadUInt32();
            lines.Add($"  Entry count: {entryCount}");

            // For each entry, try to find the scene name and then understand the state
            for (uint i = 0; i < Math.Min(entryCount, 5) && ms.Position < ms.Length - 4; i++)
            {
                var entryStart = ms.Position;
                var strLen = br.ReadInt32();
                if (strLen <= 0 || strLen > 500 || ms.Position + strLen > ms.Length)
                {
                    lines.Add($"  Entry {i}: Bad strLen={strLen} at {entryStart}");
                    break;
                }
                var name = Encoding.ASCII.GetString(br.ReadBytes(strLen));
                lines.Add($"\n  Entry {i}: '{name}' (name at {entryStart}, data at {ms.Position})");

                // Hypothesis: the scene state is preceded by a u32 byte count
                var stateStart = ms.Position;
                if (ms.Length - ms.Position < 4) break;

                // Read the state data structure
                // Common Telltale scene save formats:
                // 1. u32(blockSize) + blockData
                // 2. Telltale "SceneSave" = serialized agent states
                // Let's just dump what we see and look for patterns

                var remaining = new byte[Math.Min(512, (int)(ms.Length - ms.Position))];
                ms.Read(remaining, 0, remaining.Length);
                ms.Position = stateStart;

                // Dump hex + ascii for first 256 bytes
                lines.Add($"    State data ({remaining.Length} bytes shown):");
                for (int row = 0; row < Math.Min(256, remaining.Length); row += 32)
                {
                    var chunk = remaining.Skip(row).Take(32).ToArray();
                    var hex = BitConverter.ToString(chunk);
                    var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                    lines.Add($"      {row + stateStart:X6}: {hex}");
                    lines.Add($"               {ascii}");
                }

                // Try hypothesis: u32 size prefix
                var blockSize = BitConverter.ToUInt32(remaining, 0);
                lines.Add($"    H1 (u32 size prefix): {blockSize}");
                if (blockSize > 0 && blockSize < defData.Length && stateStart + 4 + blockSize <= defData.Length)
                {
                    lines.Add($"    -> Would skip to offset {stateStart + 4 + blockSize}");
                    // Check if the next thing after blockSize bytes looks like a valid string length + ASCII
                    var nextOffset = (int)(stateStart + 4 + blockSize);
                    if (nextOffset + 8 <= defData.Length)
                    {
                        var nextStrLen = BitConverter.ToInt32(defData, nextOffset);
                        if (nextStrLen > 0 && nextStrLen < 500 && nextOffset + 4 + nextStrLen <= defData.Length)
                        {
                            var nextName = Encoding.ASCII.GetString(defData, nextOffset + 4, nextStrLen);
                            var isAscii = nextName.All(c => c >= 32 && c < 127);
                            lines.Add($"    -> Next entry looks like: strLen={nextStrLen}, name='{(isAscii ? nextName : "[non-ASCII]")}'");
                            if (isAscii)
                            {
                                lines.Add($"    *** H1 CONFIRMED: u32 size prefix works! ***");
                                ms.Position = stateStart + 4 + blockSize;
                                continue;
                            }
                        }
                    }
                }

                // Try hypothesis: no size prefix, scan for next valid scene name
                // Scene names typically contain ".lua" or start with recognizable path patterns
                lines.Add($"    H2 (scan for next string): looking for next valid entry...");
                for (int offset = 4; offset < Math.Min(remaining.Length - 8, 4096); offset++)
                {
                    var candidateLen = BitConverter.ToInt32(remaining, offset);
                    if (candidateLen > 3 && candidateLen < 300 && offset + 4 + candidateLen <= remaining.Length)
                    {
                        var candidateStr = Encoding.ASCII.GetString(remaining, offset + 4, candidateLen);
                        if (candidateStr.All(c => c >= 32 && c < 127) &&
                            (candidateStr.Contains(".lua") || candidateStr.Contains("scene") ||
                             candidateStr.Contains("/") || candidateStr.Contains("\\")))
                        {
                            lines.Add($"    -> Found at relative offset {offset}: '{candidateStr}'");
                            lines.Add($"    -> State block size would be: {offset} bytes");
                            ms.Position = stateStart + offset;
                            break;
                        }
                    }
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Once we know the delimitation, parse ALL scene entries and catalog the data.
    /// Look for any entries whose scene state might encode player choices.
    /// </summary>
    [Fact]
    public void CatalogAllSceneEntries()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Check ALL bundle files for default.save
            var allFiles = Directory.GetFiles(dir, "*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .ToList();

            lines.Add($"\n=== {season}: {allFiles.Count} bundle files ===");

            foreach (var file in allFiles)
            {
                var slot = BundleReader.Read(file);
                if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) != true) continue;

                var defData = ExtractDefaultSection(raw);
                if (defData == null) continue;

                using var ms = new MemoryStream(defData);
                using var br = new BinaryReader(ms);

                var entryCount = br.ReadUInt32();
                lines.Add($"\n  {Path.GetFileName(file)}: {entryCount} entries, {defData.Length} bytes");

                var scenes = new List<(string name, long stateSize)>();
                bool sizePreFixed = false;

                for (uint i = 0; i < entryCount && ms.Position < ms.Length - 4; i++)
                {
                    var strLen = br.ReadInt32();
                    if (strLen <= 0 || strLen > 500 || ms.Position + strLen > ms.Length) break;
                    var name = Encoding.ASCII.GetString(br.ReadBytes(strLen));

                    // Try u32 size prefix to skip state data
                    if (ms.Position + 4 > ms.Length) break;
                    var blockSize = br.ReadUInt32();
                    if (blockSize <= ms.Length - ms.Position)
                    {
                        scenes.Add((name, blockSize));
                        ms.Position += blockSize;
                        sizePreFixed = true;
                    }
                    else
                    {
                        lines.Add($"    Entry {i}: '{name}' blockSize={blockSize} exceeds remaining={ms.Length - ms.Position}");
                        sizePreFixed = false;
                        break;
                    }
                }

                if (sizePreFixed)
                {
                    lines.Add($"    Size-prefixed parsing: {scenes.Count}/{entryCount} entries OK");

                    // Show unique scene name patterns
                    var choiceRelated = scenes
                        .Where(s => s.name.Contains("choice", StringComparison.OrdinalIgnoreCase) ||
                                    s.name.Contains("stats", StringComparison.OrdinalIgnoreCase) ||
                                    s.name.Contains("decision", StringComparison.OrdinalIgnoreCase) ||
                                    s.name.Contains("flag", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (choiceRelated.Count > 0)
                    {
                        lines.Add($"    Choice/decision related scenes ({choiceRelated.Count}):");
                        foreach (var (name, size) in choiceRelated)
                            lines.Add($"      {name} ({size} bytes)");
                    }

                    // Show all unique names
                    var uniqueNames = scenes.Select(s => s.name).Distinct().OrderBy(n => n).ToList();
                    lines.Add($"    Unique scene names ({uniqueNames.Count}):");
                    foreach (var name in uniqueNames.Take(50))
                        lines.Add($"      {name}");
                    if (uniqueNames.Count > 50)
                        lines.Add($"      ... ({uniqueNames.Count - 50} more)");
                }

                break; // Just the first file per season
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Search all inner files from S3/S4/Michonne for any that contain recognizable
    /// choice/decision strings, regardless of format.
    /// </summary>
    [Fact]
    public void SearchAllInnerFilesForChoiceStrings()
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
            if (slot.RawInnerFiles == null) continue;

            lines.Add($"\n=== {season}: {slot.RawInnerFiles.Count} inner files ===");
            int filesWithStrings = 0;

            // Search EVERY inner file for interesting strings
            foreach (var (name, data) in slot.RawInnerFiles)
            {
                if (data.Length < 10) continue;

                // Extract all printable ASCII strings >= 5 chars
                var strings = ExtractStrings(data, 5);
                var interesting = strings.Where(s =>
                    s.Contains("choice", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("decision", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("flag_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("saved_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("killed_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("_alive", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("_dead", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("_choice", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("romance", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("relationship", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("clem_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("kenny_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("javi_", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("_ending", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (interesting.Count > 0)
                {
                    var printName = name.All(c => c >= 32 && c < 127) ? name : $"[hash-{data.Length}b]";
                    lines.Add($"\n  {printName} ({data.Length} bytes): {interesting.Count} interesting strings");
                    foreach (var s in interesting.Take(30))
                        lines.Add($"    \"{s}\"");
                    if (interesting.Count > 30) lines.Add($"    ... ({interesting.Count - 30} more)");
                    filesWithStrings++;
                }
            }

            lines.Add($"\n  Total files with interesting strings: {filesWithStrings}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Deep-dive into scene state binary data for entries that look choice-related.
    /// Parse the internal structure of scene state blocks.
    /// </summary>
    [Fact]
    public void ParseSceneStateBlocks()
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

            lines.Add($"\n=== {season}: default.save ({defData.Length} bytes) ===");

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

                var stateData = br.ReadBytes((int)blockSize);

                // Only analyze blocks that might contain choice data
                // Also dump ALL blocks with their names so we can see what's there
                bool isInteresting = name.Contains("choice", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("stat", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("decision", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("logic", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("flag", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("global", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("game", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("save", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("story", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("menu", StringComparison.OrdinalIgnoreCase);

                if (isInteresting || i < 5)
                {
                    lines.Add($"\n  [{i}] '{name}' ({blockSize} bytes)");

                    if (blockSize > 0)
                    {
                        // Dump first 128 bytes hex + ASCII
                        for (int row = 0; row < Math.Min(128, (int)blockSize); row += 32)
                        {
                            var chunk = stateData.Skip(row).Take(32).ToArray();
                            var hex = BitConverter.ToString(chunk);
                            var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                            lines.Add($"    {row:X4}: {hex}");
                            lines.Add($"          {ascii}");
                        }

                        // Extract strings from this block
                        var strings = ExtractStrings(stateData, 4);
                        if (strings.Count > 0)
                        {
                            lines.Add($"    Strings ({strings.Count}):");
                            foreach (var s in strings.Take(20))
                                lines.Add($"      \"{s}\"");
                        }

                        // Try to interpret as PropertySet
                        if (blockSize >= 12)
                        {
                            TryParseAsPropertySet(stateData, lines);
                        }
                    }
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Check ALL inner PropertySet files (not just default.save) for string values
    /// that look like they store game state or choices.
    /// </summary>
    [Fact]
    public void DumpAllPropertySetStringValues()
    {
        var lines = new List<string>();
        var psReader = new PropertySetReader();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            if (slot.RawInnerFiles == null) continue;

            lines.Add($"\n=== {season}: checking all {slot.RawInnerFiles.Count} inner files ===");

            foreach (var (name, data) in slot.RawInnerFiles)
            {
                if (data.Length < 20) continue;
                var magic = BitConverter.ToUInt32(data, 0);
                if (magic is not (0x4D535635 or 0x4D535636)) continue;

                try
                {
                    var innerDefData = ExtractInnerDefaultSection(data);
                    if (innerDefData == null || innerDefData.Length < 12) continue;

                    var ps = psReader.Read(innerDefData);
                    var printName = name.All(c => c >= 32 && c < 127) ? name : $"[hash-{data.Length}b]";

                    var stringValues = new List<string>();
                    var boolValues = new List<(ulong key, bool val)>();
                    var intValues = new List<(ulong key, int val)>();

                    foreach (var group in ps.TypeGroups)
                    {
                        foreach (var prop in group.Properties)
                        {
                            if (prop.Value is StringValue sv && !string.IsNullOrEmpty(sv.Value))
                                stringValues.Add($"0x{prop.KeySymbol.Value:X16} = \"{sv.Value}\"");
                            if (prop.Value is BoolValue bv)
                                boolValues.Add((prop.KeySymbol.Value, bv.Value));
                            if (prop.Value is IntValue iv)
                                intValues.Add((prop.KeySymbol.Value, iv.Value));
                        }
                    }

                    if (stringValues.Count > 0 || boolValues.Count > 5 || intValues.Count > 0)
                    {
                        lines.Add($"\n  {printName}: {ps.TypeGroups.Count} groups");
                        foreach (var g in ps.TypeGroups)
                        {
                            var typeLabel = g.TypeSymbol.Value switch
                            {
                                0xCD9C6E605F5AF4B4 => "String",
                                0x9004C5587575D6C0 => "bool",
                                0x7CACEEBCD26D075C => "int32",
                                _ => $"0x{g.TypeSymbol.Value:X16}"
                            };
                            lines.Add($"    Group: {typeLabel} ({g.Properties.Count} props)");
                        }

                        if (stringValues.Count > 0)
                        {
                            lines.Add($"    Strings ({stringValues.Count}):");
                            foreach (var s in stringValues.Take(20))
                                lines.Add($"      {s}");
                        }
                        if (boolValues.Count > 0)
                        {
                            lines.Add($"    Bools ({boolValues.Count}):");
                            foreach (var (key, val) in boolValues.Take(10))
                                lines.Add($"      0x{key:X16} = {val}");
                        }
                        if (intValues.Count > 0)
                        {
                            lines.Add($"    Ints ({intValues.Count}):");
                            foreach (var (key, val) in intValues.Take(10))
                                lines.Add($"      0x{key:X16} = {val}");
                        }
                    }
                }
                catch { }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    /// <summary>
    /// Look at the SLOT files (not checkpoint) for S3/S4/Michonne.
    /// Maybe slot files have a different structure than checkpoints.
    /// </summary>
    [Fact]
    public void ExamineSlotFileStructure()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Slot files don't start with underscore
            var slotFiles = Directory.GetFiles(dir, "wd*.bundle", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            lines.Add($"\n=== {season}: {slotFiles.Count} slot files ===");

            foreach (var file in slotFiles)
            {
                var slot = BundleReader.Read(file);
                lines.Add($"\n  {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)");
                lines.Add($"    FileTable: {slot.FileTable.Count} entries");
                lines.Add($"    Choices: {(slot.Choices != null ? "YES" : "NO")}");
                lines.Add($"    Metadata: {(slot.Metadata != null ? "YES" : "NO")}");

                // List all inner file names
                foreach (var entry in slot.FileTable)
                    lines.Add($"    Inner: {entry.Name} ({entry.Size} bytes)");

                // If there's metadata, dump its string values
                if (slot.Metadata != null)
                {
                    lines.Add($"    Metadata groups:");
                    foreach (var g in slot.Metadata.TypeGroups)
                    {
                        foreach (var p in g.Properties)
                        {
                            if (p.Value is StringValue sv)
                                lines.Add($"      0x{p.KeySymbol.Value:X16} = \"{sv.Value}\"");
                            else if (p.Value is IntValue iv)
                                lines.Add($"      0x{p.KeySymbol.Value:X16} = {iv.Value}");
                            else if (p.Value is BoolValue bv)
                                lines.Add($"      0x{p.KeySymbol.Value:X16} = {bv.Value}");
                        }
                    }
                }

                // For any unknown inner files, dump first bytes
                if (slot.RawInnerFiles != null)
                {
                    foreach (var (name, data) in slot.RawInnerFiles)
                    {
                        if (name == "metadata_slot.p") continue;
                        var magic = data.Length >= 4 ? BitConverter.ToUInt32(data, 0) : 0u;
                        lines.Add($"    {name}: {data.Length} bytes, magic=0x{magic:X8}");
                        lines.Add($"      First 64: {BitConverter.ToString(data.Take(64).ToArray())}");
                    }
                }
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
            reader.ReadUInt32(); // dbg
            reader.ReadUInt32(); // async
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

    private static byte[]? ExtractInnerDefaultSection(byte[] innerData)
    {
        try
        {
            using var ms = new MemoryStream(innerData);
            using var reader = new BinaryReaderEx(ms);
            reader.ReadUInt32(); // magic
            var defSize = reader.ReadUInt32();
            reader.ReadUInt32(); // dbg
            reader.ReadUInt32(); // async
            var verCount = reader.ReadUInt32();
            for (uint i = 0; i < verCount; i++) { reader.ReadUInt64(); reader.ReadUInt32(); }

            bool compressed = (defSize & 0x80000000) != 0;
            var rawSize = (int)(defSize & 0x7FFFFFFF);
            if (rawSize == 0 || reader.Remaining < rawSize) return null;
            var rawBytes = reader.ReadBytes(rawSize);

            if (!compressed) return rawBytes;
            if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A) return null; // skip TTCZ for now

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
        br.ReadUInt32(); // magic
        var windowSize = br.ReadUInt32();
        var pageCount = br.ReadUInt32();
        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++)
            offsets[i] = br.ReadUInt64();

        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            var compStart = (long)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            ms.Position = compStart;
            var compressedBlock = br.ReadBytes(compLen);
            using var compStream = new MemoryStream(compressedBlock);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            var pageBuffer = new byte[windowSize];
            int totalRead = 0, bytesRead;
            while ((bytesRead = deflate.Read(pageBuffer, totalRead, (int)windowSize - totalRead)) > 0)
                totalRead += bytesRead;
            output.Write(pageBuffer, 0, totalRead);
        }
        return output.ToArray();
    }

    private static List<string> ExtractStrings(byte[] data, int minLength)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (b >= 32 && b < 127)
            {
                sb.Append((char)b);
            }
            else
            {
                if (sb.Length >= minLength)
                    result.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length >= minLength)
            result.Add(sb.ToString());
        return result;
    }

    private void TryParseAsPropertySet(byte[] data, List<string> lines)
    {
        try
        {
            var psReader = new PropertySetReader();
            var ps = psReader.Read(data);
            lines.Add($"    -> Valid PropertySet! v{ps.Version} flags=0x{ps.Flags:X} groups={ps.TypeGroups.Count}");
            foreach (var g in ps.TypeGroups)
                lines.Add($"       Group 0x{g.TypeSymbol.Value:X16}: {g.Properties.Count} props");
        }
        catch { }
    }
}
