using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Core.Tests;

public class AllSavesIntegrationTests
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    private static IEnumerable<string> AllBundleFiles()
    {
        if (!Directory.Exists(SaveDir))
            return [];
        return Directory.GetFiles(SaveDir, "*.bundle", SearchOption.AllDirectories);
    }

    [Fact]
    public void AllBundleFiles_ParseWithoutCrash()
    {
        var files = AllBundleFiles().ToList();
        if (files.Count == 0) return;

        var crashes = new List<string>();
        int withChoices = 0;
        int withoutChoices = 0;

        foreach (var file in files)
        {
            try
            {
                var slot = BundleReader.Read(file);
                if (slot.Choices != null)
                    withChoices++;
                else
                    withoutChoices++;
            }
            catch (Exception ex)
            {
                crashes.Add($"{Relative(file)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(crashes.Count == 0,
            $"Parsed {withChoices + withoutChoices}/{files.Count} files ({withChoices} with choices, {withoutChoices} without). " +
            $"{crashes.Count} crash(es):\n" +
            string.Join("\n", crashes.Take(20)));
    }

    [Fact]
    public void S2SlotFiles_HaveChoicesFromSeason1Prop()
    {
        var s2Dir = Path.Combine(SaveDir, "S2");
        if (!Directory.Exists(s2Dir)) return;

        var slotFiles = Directory.GetFiles(s2Dir, "wd*.bundle", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();
        if (slotFiles.Count == 0) return;

        foreach (var file in slotFiles)
        {
            var slot = BundleReader.Read(file);
            Assert.NotNull(slot.Choices);
            Assert.Equal("season1.prop", slot.ChoicesFileName);

            var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
            var choices = accessor.GetAllChoices();
            Assert.NotEmpty(choices);
        }
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void DumpCheckpointInnerFiles()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Pick largest autosave (most likely to have all the data)
            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            var slot = BundleReader.Read(file);
            var readableNames = slot.FileTable
                .Where(e => e.Name.All(c => c >= 32 && c < 127))
                .Select(e => e.Name)
                .ToList();

            lines.Add($"\n=== {season}: {Relative(file)} ({new FileInfo(file).Length} bytes) ===");
            lines.Add($"  Total inner files: {slot.FileTable.Count}, readable: {readableNames.Count}");
            lines.Add($"  Choices parsed: {(slot.Choices != null ? "YES" : "NO")}");

            // Show all readable names that might be choice/season related
            var interesting = readableNames
                .Where(n => n.Contains("choice", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("season", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("stat", StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(".prop") || n.EndsWith(".pro") ||
                            n.Contains("save"))
                .ToList();
            lines.Add($"  Interesting names ({interesting.Count}):");
            foreach (var name in interesting)
                lines.Add($"    {name}");

            // Show first 20 readable names
            lines.Add($"  First 20 readable names:");
            foreach (var name in readableNames.Take(20))
                lines.Add($"    {name}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void DumpDefaultSaveContents()
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
            lines.Add($"\n=== {season}: {Relative(file)} ===");

            // Try to parse default.save
            if (slot.RawInnerFiles?.TryGetValue("default.save", out var raw) == true)
            {
                lines.Add($"  default.save: {raw.Length} bytes");
                try
                {
                    // Extract default section from inner MetaStream
                    using var ms = new MemoryStream(raw);
                    using var reader = new BinaryReaderEx(ms);
                    var magic = reader.ReadUInt32();
                    var defSize = reader.ReadUInt32();
                    var dbgSize = reader.ReadUInt32();
                    var asyncSize = reader.ReadUInt32();
                    var verCount = reader.ReadUInt32();
                    lines.Add($"  Inner header: magic=0x{magic:X8}, defSize=0x{defSize:X8}, dbgSize=0x{dbgSize:X8}, asyncSize=0x{asyncSize:X8}, verCount={verCount}");

                    for (uint i = 0; i < verCount; i++) { reader.ReadUInt64(); reader.ReadUInt32(); }

                    bool compressed = (defSize & 0x80000000) != 0;
                    var rawSize = (int)(defSize & 0x7FFFFFFF);
                    var rawBytes = reader.ReadBytes(rawSize);
                    lines.Add($"  Default section: {rawSize} bytes, compressed={compressed}");

                    byte[] propData;
                    if (compressed)
                    {
                        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
                        {
                            lines.Add("  TTCZ compressed — skipping deep parse");
                            lines.Add($"  Raw first 32: {BitConverter.ToString(rawBytes.Take(32).ToArray())}");
                            continue;
                        }
                        using var cs = new MemoryStream(rawBytes);
                        using var zlib = new System.IO.Compression.ZLibStream(cs, System.IO.Compression.CompressionMode.Decompress);
                        using var output = new MemoryStream();
                        zlib.CopyTo(output);
                        propData = output.ToArray();
                    }
                    else
                    {
                        propData = rawBytes;
                    }

                    lines.Add($"  Decompressed: {propData.Length} bytes");

                    var ps = psReader.Read(propData);
                    lines.Add($"  PropertySet v{ps.Version} flags=0x{ps.Flags:X} groups={ps.TypeGroups.Count}");

                    foreach (var g in ps.TypeGroups)
                    {
                        var typeLabel = g.TypeSymbol.Value switch
                        {
                            0x8AD17AD4CB809956 => "ChoicesContainer",
                            0xCD9C6E605F5AF4B4 => "String",
                            0x7CACEEBCD26D075C => "int32",
                            0x9004C5587575D6C0 => "bool",
                            _ => $"0x{g.TypeSymbol.Value:X16}"
                        };
                        lines.Add($"  Type {typeLabel} ({g.Properties.Count} props)");

                        if (g.TypeSymbol.Value == 0x8AD17AD4CB809956)
                        {
                            foreach (var p in g.Properties.Take(3))
                            {
                                if (p.Value is TwdSaveEditor.Core.Model.RawBytesValue rbv)
                                {
                                    lines.Add($"    Key 0x{p.KeySymbol.Value:X16}: {rbv.Data.Length} bytes");
                                    // Try decode
                                    try
                                    {
                                        using var dMs = new MemoryStream(rbv.Data);
                                        using var dBr = new BinaryReader(dMs);
                                        var count = dBr.ReadUInt32();
                                        lines.Add($"      Count: {count}, first 3 entries:");
                                        for (int j = 0; j < Math.Min(count, 3); j++)
                                        {
                                            var len = dBr.ReadInt32();
                                            var chars = dBr.ReadBytes(len);
                                            var boolByte = dBr.ReadByte();
                                            lines.Add($"        \"{System.Text.Encoding.ASCII.GetString(chars)}\" = {(boolByte == 0x31)}");
                                        }
                                    }
                                    catch { lines.Add("      (decode failed)"); }
                                }
                            }
                            if (g.Properties.Count > 3)
                                lines.Add($"    ... ({g.Properties.Count - 3} more)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"  Parse error: {ex.GetType().Name}: {ex.Message}");
                }
            }
            else
            {
                lines.Add("  No default.save found");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void SearchCheckpointsForChoices()
    {
        var lines = new List<string>();
        var psReader = new PropertySetReader();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Check all bundle files
            var allFiles = Directory.GetFiles(dir, "*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .ToList();

            lines.Add($"\n=== {season}: {allFiles.Count} total bundle files ===");

            foreach (var file in allFiles)
            {
                try
                {
                    var slot = BundleReader.Read(file);

                    // Already has choices?
                    if (slot.Choices != null)
                    {
                        var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
                        var choices = accessor.GetAllChoices();
                        lines.Add($"  HAS CHOICES: {Relative(file)} ({choices.Count} choices via {slot.ChoicesFileName})");
                        foreach (var (key, value) in choices.Take(5))
                            lines.Add($"    {key} = {value}");
                        if (choices.Count > 5) lines.Add($"    ... ({choices.Count - 5} more)");
                        continue;
                    }

                    // Search all inner files for ChoicesContainer type data
                    if (slot.RawInnerFiles == null) continue;

                    foreach (var (name, rawData) in slot.RawInnerFiles)
                    {
                        if (rawData.Length < 20) continue;
                        try
                        {
                            using var ms = new MemoryStream(rawData);
                            using var reader = new BinaryReaderEx(ms);
                            var magic = reader.ReadUInt32();
                            if (magic is not (0x4D535635 or 0x4D535636)) continue;

                            var defSz = reader.ReadUInt32();
                            var dbgSz = reader.ReadUInt32();
                            var asyncSz = reader.ReadUInt32();
                            var verCnt = reader.ReadUInt32();
                            for (uint v = 0; v < verCnt; v++) { reader.ReadUInt64(); reader.ReadUInt32(); }

                            bool comp = (defSz & 0x80000000) != 0;
                            var sz = (int)(defSz & 0x7FFFFFFF);
                            if (sz == 0 || reader.Remaining < sz) continue;
                            var raw2 = reader.ReadBytes(sz);

                            byte[] propData;
                            if (comp)
                            {
                                if (raw2.Length >= 4 && BitConverter.ToUInt32(raw2, 0) == 0x5454435A) continue;
                                using var cs = new MemoryStream(raw2);
                                using var zlib = new System.IO.Compression.ZLibStream(cs, System.IO.Compression.CompressionMode.Decompress);
                                using var o = new MemoryStream();
                                zlib.CopyTo(o);
                                propData = o.ToArray();
                            }
                            else
                                propData = raw2;

                            var ps = psReader.Read(propData);
                            var hasChoicesType = ps.TypeGroups.Any(g => g.TypeSymbol.Value == 0x8AD17AD4CB809956);
                            if (hasChoicesType)
                            {
                                lines.Add($"  FOUND ChoicesContainer in: {Relative(file)} -> {name}");
                                var choicesGroup = ps.TypeGroups.First(g => g.TypeSymbol.Value == 0x8AD17AD4CB809956);
                                lines.Add($"    {choicesGroup.Properties.Count} properties");

                                // Quick decode first property
                                if (choicesGroup.Properties.Count > 0 &&
                                    choicesGroup.Properties[0].Value is TwdSaveEditor.Core.Model.RawBytesValue rbv)
                                {
                                    using var dMs = new MemoryStream(rbv.Data);
                                    using var dBr = new BinaryReader(dMs);
                                    var count = dBr.ReadUInt32();
                                    lines.Add($"    First prop: {count} entries");
                                    for (int j = 0; j < Math.Min(count, 5); j++)
                                    {
                                        var len = dBr.ReadInt32();
                                        var chars = dBr.ReadBytes(len);
                                        var bb = dBr.ReadByte();
                                        lines.Add($"      \"{System.Text.Encoding.ASCII.GetString(chars)}\"");
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"  ERROR: {Relative(file)}: {ex.GetType().Name}");
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic — no choice data found in S3/S4/Michonne")]
    public void BruteSearchForChoiceStrings()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            lines.Add($"\n=== {season} ===");

            // Search ALL files (bundles, estores, epages)
            var allFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .Take(10);

            foreach (var file in allFiles)
            {
                var raw = File.ReadAllBytes(file);
                var ext = Path.GetExtension(file).ToLower();

                // Search for known choice patterns in raw bytes
                var text = System.Text.Encoding.ASCII.GetString(raw);

                // Look for "key - value" patterns (with " - " separator)
                var matches = new HashSet<string>();
                int idx = 0;
                while ((idx = text.IndexOf(" - ", idx, StringComparison.Ordinal)) >= 0)
                {
                    // Extract surrounding context
                    int start = idx - 1;
                    while (start > 0 && text[start] >= 32 && text[start] < 127 && text[start] != '\0') start--;
                    start++;
                    int end = idx + 3;
                    while (end < text.Length && text[end] >= 32 && text[end] < 127 && text[end] != '\0') end++;

                    var match = text[start..end];
                    if (match.Length > 5 && match.Length < 100 && !match.Contains("http") && !match.Contains("//"))
                        matches.Add(match);
                    idx++;
                }

                if (matches.Count > 0)
                {
                    // Filter for things that look like choice entries
                    var choiceLike = matches
                        .Where(m => m.Contains("_") && m.IndexOf(" - ") > 3) // key must have underscores
                        .OrderBy(m => m)
                        .ToList();

                    if (choiceLike.Count > 0)
                    {
                        lines.Add($"\n  {Relative(file)} ({ext}, {raw.Length} bytes): {choiceLike.Count} choice-like entries");
                        foreach (var m in choiceLike.Take(20))
                            lines.Add($"    {m}");
                        if (choiceLike.Count > 20) lines.Add($"    ... ({choiceLike.Count - 20} more)");
                    }
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic — no choice data found in S3/S4/Michonne")]
    public void BruteSearchDecompressedCheckpoints()
    {
        var lines = new List<string>();

        foreach (var season in new[] { "S3", "S4", "Michonne" })
        {
            var dir = Path.Combine(SaveDir, season);
            if (!Directory.Exists(dir)) continue;

            // Largest checkpoint/autosave
            var file = Directory.GetFiles(dir, "_wd*.bundle", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (file == null) continue;

            lines.Add($"\n=== {season}: {Relative(file)} ===");

            var slot = BundleReader.Read(file);
            lines.Add($"  Inner files: {slot.FileTable.Count}");

            // Search through all raw inner file data for choice patterns
            if (slot.RawInnerFiles != null)
            {
                foreach (var (name, data) in slot.RawInnerFiles)
                {
                    // Search decompressed data for "key - value" patterns
                    var text = System.Text.Encoding.ASCII.GetString(data);
                    var matches = new List<string>();

                    int idx = 0;
                    while ((idx = text.IndexOf(" - ", idx, StringComparison.Ordinal)) >= 0)
                    {
                        int start = idx - 1;
                        while (start > 0 && text[start] >= 32 && text[start] < 127) start--;
                        start++;
                        int end = idx + 3;
                        while (end < text.Length && text[end] >= 32 && text[end] < 127) end++;

                        var match = text[start..end];
                        if (match.Length > 5 && match.Length < 100 && match.Contains("_"))
                            matches.Add(match);
                        idx++;
                    }

                    if (matches.Count > 0)
                    {
                        var printName = name.All(c => c >= 32 && c < 127) ? name : $"[{data.Length}b]";
                        lines.Add($"  {printName}: {matches.Count} matches");
                        foreach (var m in matches.Distinct().Take(10))
                            lines.Add($"    {m}");
                    }
                }
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void ExamineDefaultSaveRawFormat()
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

            lines.Add($"\n=== {season}: default.save from {Relative(file)} ({raw.Length} bytes) ===");

            // Parse inner MetaStream header
            using var ms = new MemoryStream(raw);
            using var reader = new BinaryReaderEx(ms);
            var magic = reader.ReadUInt32();
            var defSize = reader.ReadUInt32();
            var dbgSize = reader.ReadUInt32();
            var asyncSize = reader.ReadUInt32();
            var verCount = reader.ReadUInt32();
            lines.Add($"  magic=0x{magic:X8} defSize=0x{defSize:X8} dbgSize=0x{dbgSize:X8} asyncSize=0x{asyncSize:X8} vers={verCount}");

            for (uint i = 0; i < verCount; i++)
            {
                var tc = reader.ReadUInt64();
                var vc = reader.ReadUInt32();
                lines.Add($"  Version: type=0x{tc:X16} ver=0x{vc:X8}");
            }

            var sz = (int)(defSize & 0x7FFFFFFF);
            var defData = reader.ReadBytes(sz);
            lines.Add($"  Default section: {defData.Length} bytes");
            lines.Add($"  First 128 bytes hex:");
            for (int row = 0; row < Math.Min(128, defData.Length); row += 32)
            {
                var chunk = defData.Skip(row).Take(32).ToArray();
                var hex = BitConverter.ToString(chunk);
                var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                lines.Add($"    {row:X4}: {hex}  {ascii}");
            }

            // Check debug section
            var dbgSz = (int)(dbgSize & 0x7FFFFFFF);
            if (dbgSz > 0 && reader.Remaining >= dbgSz)
            {
                var dbgData = reader.ReadBytes(dbgSz);
                lines.Add($"  Debug section: {dbgData.Length} bytes");
                lines.Add($"  Debug first 128 bytes:");
                for (int row = 0; row < Math.Min(128, dbgData.Length); row += 32)
                {
                    var chunk = dbgData.Skip(row).Take(32).ToArray();
                    var hex = BitConverter.ToString(chunk);
                    var ascii = new string(chunk.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                    lines.Add($"    {row:X4}: {hex}  {ascii}");
                }

                // Try to interpret as a list of null-terminated strings
                var strings = new List<string>();
                var sb = new System.Text.StringBuilder();
                foreach (var b in dbgData)
                {
                    if (b == 0)
                    {
                        if (sb.Length > 0) { strings.Add(sb.ToString()); sb.Clear(); }
                    }
                    else if (b >= 32 && b < 127) sb.Append((char)b);
                    else { if (sb.Length > 0) { strings.Add(sb.ToString()); sb.Clear(); } }
                }
                if (sb.Length > 0) strings.Add(sb.ToString());

                lines.Add($"  Debug strings ({strings.Count}):");
                foreach (var s in strings.Where(s => s.Length > 3).Take(40))
                    lines.Add($"    \"{s}\"");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact]
    public void ScanAllInnerFilesForPropertySets()
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
            lines.Add($"\n=== {season}: {slot.FileTable.Count} inner files ===");

            int propSetsFound = 0;
            int choicesContainerFound = 0;

            if (slot.RawInnerFiles != null)
            {
                foreach (var (name, data) in slot.RawInnerFiles)
                {
                    if (data.Length < 20) continue;

                    // Check if it starts with MSV5/MSV6 magic
                    var magic = BitConverter.ToUInt32(data, 0);
                    if (magic is not (0x4D535635 or 0x4D535636)) continue;

                    try
                    {
                        // Extract default section
                        using var ms = new MemoryStream(data);
                        using var reader = new BinaryReaderEx(ms);
                        reader.ReadUInt32(); // magic
                        var defSz = reader.ReadUInt32();
                        reader.ReadUInt32(); // dbg
                        reader.ReadUInt32(); // async
                        var vc = reader.ReadUInt32();
                        for (uint v = 0; v < vc; v++) { reader.ReadUInt64(); reader.ReadUInt32(); }

                        var rawSz = (int)(defSz & 0x7FFFFFFF);
                        if (rawSz == 0 || reader.Remaining < rawSz) continue;

                        // Skip compressed inner files for now
                        if ((defSz & 0x80000000) != 0) continue;

                        var propData = reader.ReadBytes(rawSz);
                        var ps = psReader.Read(propData);
                        propSetsFound++;

                        // Check for ChoicesContainer
                        var choicesGroup = ps.TypeGroups.FirstOrDefault(g => g.TypeSymbol.Value == 0x8AD17AD4CB809956);
                        if (choicesGroup != null)
                        {
                            choicesContainerFound++;
                            var printName = name.All(c => c >= 32 && c < 127) ? name : $"[hash:{data.Length}b]";
                            lines.Add($"  CHOICES in {printName}: {choicesGroup.Properties.Count} props");
                        }
                    }
                    catch { }
                }
            }

            lines.Add($"  PropertySets parsed: {propSetsFound}, with ChoicesContainer: {choicesContainerFound}");
        }

        Assert.Fail(string.Join("\n", lines));
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void DumpAllUniqueChoiceKeys()
    {
        var files = AllBundleFiles().ToList();
        if (files.Count == 0) return;

        var choicesByDir = new Dictionary<string, HashSet<string>>();
        var allKeyValues = new Dictionary<string, HashSet<string>>();

        foreach (var file in files)
        {
            try
            {
                var slot = BundleReader.Read(file);
                if (slot.Choices == null) continue;

                var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
                var choices = accessor.GetAllChoices();
                var dir = GetSeasonDir(file);

                if (!choicesByDir.ContainsKey(dir))
                    choicesByDir[dir] = [];

                foreach (var (key, value) in choices)
                {
                    choicesByDir[dir].Add($"{key} = {value}");
                    if (!allKeyValues.ContainsKey(key))
                        allKeyValues[key] = [];
                    allKeyValues[key].Add(value);
                }
            }
            catch { }
        }

        var lines = new List<string>();
        lines.Add($"=== {allKeyValues.Count} unique choice keys across {files.Count} files ===");

        foreach (var dir in choicesByDir.Keys.OrderBy(k => k))
        {
            lines.Add($"\n--- {dir} ({choicesByDir[dir].Count} entries) ---");
            foreach (var entry in choicesByDir[dir].OrderBy(e => e))
                lines.Add($"  {entry}");
        }

        lines.Add("\n=== All keys with all observed values ===");
        foreach (var (key, values) in allKeyValues.OrderBy(kv => kv.Key))
            lines.Add($"  {key}: [{string.Join(", ", values.OrderBy(v => v))}]");

        Assert.Fail(string.Join("\n", lines));
    }

    private static string Relative(string path) =>
        path.Replace(SaveDir + "\\", "").Replace(SaveDir + "/", "");

    private static string GetSeasonDir(string path)
    {
        var rel = Relative(path);
        var parts = rel.Split(new[] { '\\', '/' });
        return parts.Length > 1 ? parts[0] : "root";
    }
}
