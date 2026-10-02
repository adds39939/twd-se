using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Chapters;

public static class ChapterAligner
{
    public static string[] Align(IReadOnlyList<MenuEntry> entries, IReadOnlyList<GameChapter> chapters)
    {
        var lengths = new int[entries.Count + 1, chapters.Count + 1];
        for (var entry = entries.Count - 1; entry >= 0; entry--)
        {
            for (var chapter = chapters.Count - 1; chapter >= 0; chapter--)
            {
                lengths[entry, chapter] = Matches(entries[entry], chapters[chapter])
                    ? Math.Max(1 + lengths[entry + 1, chapter + 1], Math.Max(lengths[entry + 1, chapter], lengths[entry, chapter + 1]))
                    : Math.Max(lengths[entry + 1, chapter], lengths[entry, chapter + 1]);
            }
        }

        var aligned = new string?[entries.Count];
        for (int entry = 0, chapter = 0; entry < entries.Count && chapter < chapters.Count;)
        {
            if (Matches(entries[entry], chapters[chapter]) && lengths[entry, chapter] == 1 + lengths[entry + 1, chapter + 1])
                aligned[entry++] = chapters[chapter++].ChapterId;
            else if (lengths[entry + 1, chapter] >= lengths[entry, chapter + 1])
                entry++;
            else
                chapter++;
        }

        var current = chapters.Count > 0 ? chapters[0].ChapterId : string.Empty;
        return [.. aligned.Select(chapter => current = chapter ?? current)];
    }

    private static bool Matches(MenuEntry entry, GameChapter chapter) =>
        chapter.Scripts.Contains(entry.Script, StringComparer.OrdinalIgnoreCase);
}
