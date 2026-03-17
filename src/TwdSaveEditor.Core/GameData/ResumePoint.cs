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
}
