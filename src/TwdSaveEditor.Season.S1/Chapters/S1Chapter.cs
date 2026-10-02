namespace TwdSaveEditor.Season.S1.Chapters;

public sealed record S1Chapter(string Id, string Title, string Group, string Script, S1ChapterEntry? Entry, IReadOnlyList<S1ChapterFlag> Flags)
{
    public bool StartsEpisode => Entry == null;
}
