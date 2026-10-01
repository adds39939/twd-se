using System.Text;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.ValidateSaves.Archives;
using TwdSaveEditor.Tools.ValidateSaves.Data;
using TwdSaveEditor.Tools.ValidateSaves.Lua;

namespace TwdSaveEditor.Tools.ValidateSaves.Validators;

public static class MetadataHashValidator
{
    private const string LuaFile = "SaveLoad.lua";

    private static readonly string[] AlternativeLuaFiles = ["saveload.lua", "SaveLoad.lenc", "saveload.lenc"];

    private static readonly string[] RawPatterns = ["metadata_slot", "chapterCount", "saveSlotIndex", "episodeId", "autosave", "SaveLoad"];

    private static readonly string[] MetadataPatterns =
    [
        "chapterCount", "saveSlotIndex", "SaveSlotIndex",
        "episodeId", "EpisodeId", "episode_id",
        "autosave", "isValid", "IsValid",
        "SaveSlotChapterCount", "numChaptersComplete",
    ];

    private static readonly OrderedDictionary<ulong, string[]> CandidateNames = new()
    {
        [0x7C725227A47FD1BA] = ["chapterCount", "numChaptersComplete", "SaveSlotChapterCount", "chapter_count", "ChapterCount", "mChapterCount"],
        [0x94C245DACB1ADDC3] = ["saveSlotIndex", "SaveSlotIndex", "save_slot_index", "mSaveSlotIndex", "slotIndex", "slot_index"],
        [0x4F8338150CC8BCD6] = ["isValid", "IsValid", "is_valid", "mIsValid", "bIsValid", "mValid", "valid"],
        [0xB218E7C003A67CE9] = ["episodeId", "EpisodeId", "episode_id", "mEpisodeId", "currentEpisode", "CurrentEpisode", "current_episode"],
        [0xF235E9FCE9562E01] = ["autosavePath", "AutosavePath", "autosave_path", "mAutosavePath", "checkpointFile", "savePath"],
    };

    public static List<string> Validate(SeasonInfo season, byte[]? archiveData)
    {
        var results = new List<string>();

        if (archiveData == null)
        {
            results.Add($"  SKIP: No archive data available for {season.Key}");
            return results;
        }

        var lua = FindLua(archiveData, results);
        if (lua == null)
        {
            AddRawPatternResults(results, archiveData);
            return results;
        }

        results.Add($"  Extracted SaveLoad.lua: {lua.Length} bytes");

        if (LuaDecryptor.IsEncrypted(lua))
        {
            results.Add("  Lua file is LEn encrypted, decrypting...");
            lua = LuaDecryptor.Decrypt(lua);
            results.Add($"  Decrypted: {lua.Length} bytes");
        }

        List<string> strings;
        if (LuaDecryptor.IsCompiled(lua))
        {
            results.Add("  Lua file is compiled bytecode - extracting strings only");
            strings = LuaStrings.FromBytecode(lua);
        }
        else
        {
            results.Add("  Lua file is source code");
            strings = LuaStrings.FromSource(lua);
        }

        AddFoundNameResults(results, strings);
        AddCandidateResults(results, strings);
        return results;
    }

    private static byte[]? FindLua(byte[] archiveData, List<string> results)
    {
        var lua = ArchiveDirectoryParser.Find(archiveData, LuaFile);
        if (lua != null)
            return lua;

        foreach (var alternative in AlternativeLuaFiles)
        {
            lua = ArchiveDirectoryParser.Find(archiveData, alternative);
            if (lua is not { Length: > 0 })
                continue;

            results.Add($"  Found as: {alternative}");
            break;
        }

        return lua;
    }

    private static void AddRawPatternResults(List<string> results, byte[] archiveData)
    {
        results.Add("  Could not extract SaveLoad.lua from archive");
        results.Add("  Searching for SaveLoad patterns in raw data...");

        foreach (var text in RawPatterns)
        {
            var pattern = Encoding.ASCII.GetBytes(text);
            var count = Bytes.Count(archiveData, pattern);
            if (count == 0)
                continue;

            var index = Bytes.IndexOf(archiveData, pattern);
            var context = Bytes.Slice(archiveData, index - 30, index + pattern.Length + 50);
            results.Add($"  Found '{text}' x{count}: ...{Readable(context)}...");
        }
    }

    private static void AddFoundNameResults(List<string> results, List<string> strings)
    {
        var foundNames = new List<string>();
        foreach (var pattern in MetadataPatterns)
        {
            if (!strings.Any(text => string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase)))
                continue;

            foundNames.Add(pattern);
            results.Add($"  Found metadata property: '{pattern}'");
        }

        if (foundNames.Count == 0)
        {
            results.Add("  No metadata property names found in Lua source");
            return;
        }

        results.Add($"  Computing CRC64 hashes for {foundNames.Count} found property names:");
        foreach (var name in foundNames)
        {
            var hash = TelltaleCrc64.Compute(name);
            results.Add(SaveFormat.KnownMetadataHashes.TryGetValue(hash, out var known)
                ? $"    '{name}' -> 0x{hash:X16} MATCH ({known})"
                : $"    '{name}' -> 0x{hash:X16} (not in SaveSlotFactory)");
        }
    }

    private static void AddCandidateResults(List<string> results, List<string> strings)
    {
        results.Add("\n  Reverse-engineering known hashes:");
        foreach (var (target, candidates) in CandidateNames)
        {
            var description = SaveFormat.KnownMetadataHashes[target];
            var candidate = candidates.FirstOrDefault(name => TelltaleCrc64.Compute(name) == target);
            if (candidate != null)
            {
                results.Add($"    0x{target:X16} ({description}) = CRC64('{candidate}') CONFIRMED");
                continue;
            }

            results.Add($"    0x{target:X16} ({description}) = no candidate matched");
            var luaString = strings.FirstOrDefault(text => TelltaleCrc64.Compute(text) == target);
            results.Add(luaString != null
                ? $"    0x{target:X16} ({description}) = CRC64('{luaString}') FOUND IN LUA!"
                : $"    0x{target:X16} ({description}) = NOT FOUND in Lua strings either");
        }
    }

    private static string Readable(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder(data.Length);
        foreach (var b in data)
            builder.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');

        return builder.ToString();
    }
}
