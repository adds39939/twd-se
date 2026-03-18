namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Represents where the game should resume — season, episode, and chapter.
/// Maps to properties in the save's prefs.prop or save_N.save files.
/// </summary>
public sealed class ResumePoint
{
    public string SeasonKey { get; set; } = "s1";
    public int Episode { get; set; } = 1;
    public int Chapter { get; set; } = 1;

    /// <summary>
    /// Known property keys used for resume state.
    /// The Definitive Series uses a combined structure, but individual
    /// season saves may use their own keys.
    /// </summary>
    public static class Keys
    {
        // Prefs-level keys (which season/episode to load)
        public const string ActiveSeason = "mActiveSeason";
        public const string ActiveEpisode = "mActiveEpisode";
        public const string ActiveSaveSlotIndex = "mActiveSaveSlotIndex";

        // Per-save-file keys (where within an episode)
        public const string CurrentEpisode = "mCurrentEpisode";
        public const string CurrentChapter = "mCurrentChapter";
        public const string CurrentScene = "mCurrentScene";
        public const string EpisodeNumber = "mEpisodeNumber";
        public const string ChapterNumber = "mChapterNumber";
        public const string SaveVersion = "mSaveVersion";
        public const string GameComplete = "mGameComplete";
        public const string Playtime = "mPlaytime";
    }

    /// <summary>
    /// Property hashes found in autosave metadata_save.p files.
    /// These control where the game actually resumes — the slot metadata does NOT.
    /// </summary>
    public static class AutosaveHashes
    {
        /// <summary>Episode ID string (e.g. "WalkingDead101").</summary>
        public const ulong EpisodeId = 0x7E7BE4FD8F464350;

        /// <summary>Checkpoint dialog file (e.g. "env_copcar.dlog").</summary>
        public const ulong CheckpointDialog = 0x6047826CD4EDC6B4;

        /// <summary>Checkpoint dialog node hash.</summary>
        public const ulong CheckpointNode = 0x8B8C42FEDDCE350C;

        /// <summary>Scene name (e.g. "streetOutskirts").</summary>
        public const ulong SceneName = 0x98A7E965982E6E98;
    }
}
