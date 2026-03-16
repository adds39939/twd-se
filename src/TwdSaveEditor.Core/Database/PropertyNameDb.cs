using System.Text.Json;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Database;

/// <summary>
/// Maps CRC64 symbol hashes to human-readable property names.
/// Two layers: shipped defaults from embedded JSON + user-contributed mappings.
/// </summary>
public sealed class PropertyNameDb
{
    private readonly Dictionary<ulong, string> _names = new();

    public int Count => _names.Count;

    public string? Resolve(Symbol symbol) =>
        _names.TryGetValue(symbol.Value, out var name) ? name : null;

    public string ResolveOrHex(Symbol symbol) =>
        Resolve(symbol) ?? $"0x{symbol.Value:X16}";

    public void Register(string name)
    {
        var hash = TelltaleHash.ComputeCrc64(name);
        _names[hash] = name;
    }

    public void Register(ulong hash, string name)
    {
        _names[hash] = name;
    }

    /// <summary>
    /// Load name mappings from a JSON file. Expected format:
    /// { "propertyName": "hash_hex_string", ... } or { "hash_hex_string": "propertyName", ... }
    /// We support a simple array-of-strings format (names only, hash computed):
    /// ["propertyName1", "propertyName2", ...]
    /// </summary>
    public void LoadFromJsonFile(string path)
    {
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        LoadFromJson(json);
    }

    public void LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.GetString() is { } name)
                    Register(name);
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Try: key is name, value is anything — just register the key
                Register(prop.Name);
            }
        }
    }

    /// <summary>
    /// Save user-contributed names to JSON (array-of-strings format).
    /// </summary>
    public void SaveToJsonFile(string path)
    {
        var names = _names.Values.OrderBy(n => n).ToList();
        var json = JsonSerializer.Serialize(names, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Creates a DB pre-populated with common Telltale property names.
    /// </summary>
    public static PropertyNameDb CreateDefault()
    {
        var db = new PropertyNameDb();

        // Type names
        string[] typeNames = ["bool", "int", "int32", "float", "String", "Symbol", "PropertySet", "uint64", "long",
            "Color", "Vector2", "Vector3", "Vector4", "Quaternion", "Transform", "Rect",
            "Map", "Set", "SArray", "DCArray", "List", "DArray",
            "Flags", "Handle", "Ptr"];

        foreach (var name in typeNames)
            db.Register(name);

        // Common TWD save property names
        string[] commonProps = [
            // Game state
            "mActiveSaveSlotIndex", "mGameComplete", "mCurrentEpisode", "mCurrentChapter",
            "mPlaythroughGUID", "mSaveVersion",
            // Character state
            "mbAlive", "mbDead", "mRelationship", "mDisposition",
            // Choices
            "mChoiceId", "mChoiceValue", "mChoiceMade", "mSelectedChoice",
            // Stats
            "mHealth", "mStamina", "mMorale",
            // Items
            "mInventory", "mItems", "mItemCount", "mHasItem",
            // Dialog
            "mDialogNode", "mDialogChoice", "mDialogResult",
            // Scene
            "mCurrentScene", "mSceneName", "mMapName",
            // Flags
            "mFlags", "mBooleanVarValue", "mIntVarValue", "mFloatVarValue", "mStringVarValue",
            // Prefs
            "mSubtitles", "mVolumeMaster", "mVolumeMusic", "mVolumeSFX", "mVolumeVoice",
            "mInvertY", "mSensitivityX", "mSensitivityY", "mBrightness",
            "mResolutionWidth", "mResolutionHeight", "mFullscreen", "mVSync",
            "mQualityLevel", "mShadowQuality", "mTextureQuality", "mAntiAliasing",
            // Save metadata
            "mSaveName", "mSaveTimestamp", "mPlaytime", "mEpisodeNumber", "mChapterNumber"
        ];

        foreach (var name in commonProps)
            db.Register(name);

        // Register all known choice property keys from the choice database
        RegisterChoiceKeys(db);

        return db;
    }

    private static void RegisterChoiceKeys(PropertyNameDb db)
    {
        // Resume point keys
        db.Register("mActiveSeason");
        db.Register("mActiveEpisode");

        // All choice keys
        foreach (var choice in GameData.ChoiceDatabase.AllChoices)
            db.Register(choice.ChoiceKey);
    }
}
