using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.MetaStreams;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ValidateSaves.Data;

namespace TwdSaveEditor.Tools.ValidateSaves.Validators;

public static class BundleStructureValidator
{
    public static List<string> Validate(SeasonInfo season)
    {
        var results = new List<string>();

        var bundlePath = Path.Combine(ToolPaths.TestData, season.Key, season.TestBundle);
        if (!File.Exists(bundlePath))
        {
            results.Add($"  SKIP: {bundlePath} not found");
            return results;
        }

        var data = File.ReadAllBytes(bundlePath);

        var magic = Bytes.U32(data, 0);
        var defaultField = Bytes.U32(data, 4);
        var debugField = Bytes.U32(data, 8);
        var asyncField = Bytes.U32(data, 12);
        var versionCount = Bytes.U32(data, 16);

        results.Add($"  Outer magic: {MetaStreamParser.MagicName(magic)}");
        results.Add(magic == MetaStreamParser.MagicMsv6
            ? "  MATCH: Outer uses MSV6 (matches SaveSlotFactory)"
            : $"  MISMATCH: Outer uses {MetaStreamParser.MagicName(magic)}, SaveSlotFactory uses MSV6");
        results.Add($"  Version entries: {versionCount}");

        long position = 20;
        var versionEntries = MetaStreamParser.ReadVersionEntries(data, ref position, versionCount);
        AddOuterVersionResults(results, versionEntries);

        var defaultSize = MetaStreamParser.SectionSize(defaultField);
        var table = Bytes.Slice(data, position, position + defaultSize);

        if (MetaStreamParser.IsCompressed(defaultField))
        {
            results.Add(table.StartsWith("ZCTT"u8)
                ? $"  Default section: TTCZ compressed ({defaultSize} bytes)"
                : "  Default section: compressed but not TTCZ");
            return results;
        }

        if (table.Length < 8)
        {
            return results;
        }

        var fileCount = Bytes.U32(table, 4);
        results.Add($"  File table: {fileCount} entries (unknown1={Bytes.U32(table, 0)})");

        var asyncStart = position + defaultSize + MetaStreamParser.SectionSize(debugField);
        var tablePosition = 8;
        for (uint i = 0; i < fileCount; i++)
        {
            if (tablePosition + 8 > table.Length)
            {
                break;
            }

            var offset = Bytes.U32(table, tablePosition);
            tablePosition += 8;

            var nameStart = tablePosition;
            while (tablePosition < table.Length && table[tablePosition] != 0)
            {
                tablePosition++;
            }

            var name = TextFormat.DecodeAscii(table[nameStart..tablePosition]);
            var nameLength = tablePosition - nameStart + 1;
            tablePosition += 1 + ((nameLength + 3) & ~3) - nameLength;

            if (tablePosition + 16 > table.Length)
            {
                break;
            }

            var firstHash = Bytes.U64(table, tablePosition);
            var secondHash = Bytes.U64(table, tablePosition + 8);
            tablePosition += 16;

            results.Add($"    File: {name} (hash1=0x{firstHash:X16}, hash2=0x{secondHash:X16})");
            AddFileHashResults(results, name, firstHash, secondHash);

            if (MetaStreamParser.IsCompressed(asyncField))
            {
                results.Add("      (async section compressed - inner files checked via C# tests)");
                continue;
            }

            var innerOffset = asyncStart + offset;
            if (innerOffset + 20 < data.Length)
            {
                AddInnerFileResults(results, data, innerOffset, name);
            }
        }

        return results;
    }

    private static void AddOuterVersionResults(List<string> results, List<VersionEntry> versionEntries)
    {
        var expectedEntries = SaveFormat.OuterVersionEntries;
        if (versionEntries.Count != expectedEntries.Length)
        {
            results.Add($"  MISMATCH: {versionEntries.Count} version entries, expected {expectedEntries.Length}");
            return;
        }

        var allMatch = true;
        foreach (var (real, expected) in versionEntries.Zip(expectedEntries))
        {
            if (real == expected)
            {
                continue;
            }

            allMatch = false;
            results.Add($"  MISMATCH: Version entry 0x{real.Type:X16}/0x{real.Version:X8} != expected 0x{expected.Type:X16}/0x{expected.Version:X8}");
        }

        if (allMatch)
        {
            results.Add($"  MATCH: All {versionEntries.Count} outer version entries match SaveSlotFactory");
        }
    }

    private static void AddFileHashResults(List<string> results, string name, ulong firstHash, ulong secondHash)
    {
        if (!SaveFormat.FileHashes.TryGetValue(name, out var expected))
        {
            return;
        }

        if (firstHash == expected.First && secondHash == expected.Second)
        {
            results.Add("      MATCH: Hashes match SaveSlotFactory constants");
            return;
        }

        if (firstHash != expected.First)
        {
            results.Add($"      MISMATCH: hash1 0x{firstHash:X16} != expected 0x{expected.First:X16}");
        }

        if (secondHash != expected.Second)
        {
            results.Add($"      MISMATCH: hash2 0x{secondHash:X16} != expected 0x{expected.Second:X16}");
        }
    }

    private static void AddInnerFileResults(List<string> results, ReadOnlySpan<byte> data, long innerOffset, string name)
    {
        var innerMagic = Bytes.U32(data, innerOffset);
        var innerVersionCount = Bytes.U32(data, innerOffset + 16);
        results.Add($"      Inner magic: {MetaStreamParser.MagicName(innerMagic)}, {innerVersionCount} version entries");
        results.Add(innerMagic == MetaStreamParser.MagicMsv6
            ? "      MATCH: Inner uses MSV6 (matches BuildInnerMetaStream)"
            : $"      NOTE: Inner uses {MetaStreamParser.MagicName(innerMagic)}");

        var innerPosition = innerOffset + 20;
        var innerVersions = new List<VersionEntry>();
        for (uint i = 0; i < innerVersionCount; i++)
        {
            if (innerPosition + 12 > data.Length)
            {
                break;
            }

            innerVersions.Add(new VersionEntry(Bytes.U64(data, innerPosition), Bytes.U32(data, innerPosition + 8)));
            innerPosition += 12;
        }

        var expectedEntries = SaveFormat.InnerVersionEntries;
        if (innerVersions.Count != expectedEntries.Length)
        {
            results.Add($"      NOTE: {innerVersions.Count} inner version entries vs {expectedEntries.Length} expected");
        }
        else if (innerVersions.SequenceEqual(expectedEntries))
        {
            results.Add("      MATCH: Inner version entries match InnerVersionEntries");
        }
        else
        {
            foreach (var (real, expected) in innerVersions.Zip(expectedEntries))
            {
                if (real != expected)
                {
                    results.Add($"      MISMATCH: inner ver 0x{real.Type:X16}/0x{real.Version:X8} != 0x{expected.Type:X16}/0x{expected.Version:X8}");
                }
            }
        }

        if (MetaStreamParser.IsCompressed(Bytes.U32(data, innerOffset + 4)) || innerPosition + 8 > data.Length)
        {
            return;
        }

        var version = Bytes.U32(data, innerPosition);
        var flags = Bytes.U32(data, innerPosition + 4);
        results.Add($"      PropertySet: version={version}, flags=0x{flags:X}");

        switch (name)
        {
            case "metadata_slot.p":
                results.Add(version == 2 && flags == 0x100
                    ? "      MATCH: metadata version=2, flags=0x100"
                    : $"      MISMATCH: metadata version={version} flags=0x{flags:X} (expected 2, 0x100)");
                break;
            case "choices.prop" or "season1.prop":
                results.Add(version == 2
                    ? $"      MATCH: choices version=2, flags=0x{flags:X}"
                    : $"      MISMATCH: choices version={version} (expected 2)");
                break;
            case "choicestats.pro":
                results.Add(version == 2 && flags == 0x100
                    ? "      MATCH: choicestats version=2, flags=0x100"
                    : $"      MISMATCH: choicestats version={version} flags=0x{flags:X} (expected 2, 0x100)");
                break;
        }
    }
}
