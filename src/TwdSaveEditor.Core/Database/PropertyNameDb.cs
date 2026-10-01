using System.Text.Json;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Database;

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
                Register(prop.Name);
            }
        }
    }

    public void SaveToJsonFile(string path)
    {
        var names = _names.Values.OrderBy(n => n).ToList();
        var json = JsonSerializer.Serialize(names, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static PropertyNameDb CreateDefault(IEnumerable<string>? additionalNames = null)
    {
        var db = new PropertyNameDb();

        string[] typeNames = ["bool", "int", "int32", "float", "String", "Symbol", "PropertySet", "uint64", "long",
            "Color", "Vector2", "Vector3", "Vector4", "Quaternion", "Transform", "Rect",
            "Map", "Set", "SArray", "DCArray", "List", "DArray",
            "Flags", "Handle", "Ptr"];

        foreach (var name in typeNames)
            db.Register(name);

        string[] commonProps = [
            "mActiveSaveSlotIndex", "mGameComplete", "mCurrentEpisode", "mCurrentChapter",
            "mPlaythroughGUID", "mSaveVersion",
            "mbAlive", "mbDead", "mRelationship", "mDisposition",
            "mChoiceId", "mChoiceValue", "mChoiceMade", "mSelectedChoice",
            "mHealth", "mStamina", "mMorale",
            "mInventory", "mItems", "mItemCount", "mHasItem",
            "mDialogNode", "mDialogChoice", "mDialogResult",
            "mCurrentScene", "mSceneName", "mMapName",
            "mFlags", "mBooleanVarValue", "mIntVarValue", "mFloatVarValue", "mStringVarValue",
            "mSubtitles", "mVolumeMaster", "mVolumeMusic", "mVolumeSFX", "mVolumeVoice",
            "mInvertY", "mSensitivityX", "mSensitivityY", "mBrightness",
            "mResolutionWidth", "mResolutionHeight", "mFullscreen", "mVSync",
            "mQualityLevel", "mShadowQuality", "mTextureQuality", "mAntiAliasing",
            "mSaveName", "mSaveTimestamp", "mPlaytime", "mEpisodeNumber", "mChapterNumber"
        ];

        foreach (var name in commonProps)
            db.Register(name);

        db.Register("mActiveSeason");
        db.Register("mActiveEpisode");

        string[] saveProps = [
            SlotMetadataKeys.LatestSerial, SlotMetadataKeys.LatestSave, SlotMetadataKeys.EpisodeInProgress,
            SlotMetadataKeys.SlotName, SlotMetadataKeys.Progress,
            SaveMetadataKeys.Serial, SaveMetadataKeys.Date, SaveMetadataKeys.Episode, SaveMetadataKeys.ChapterId,
            SaveMetadataKeys.CheckpointDialog, SaveMetadataKeys.CheckpointDialogNode,
        ];

        foreach (var name in saveProps)
            db.Register(name);

        foreach (var name in additionalNames ?? [])
            db.Register(name);

        return db;
    }
}
