using System.Text.RegularExpressions;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Scripts;

public static partial class CheckpointCalls
{
    public static IEnumerable<string> ChapterIds(string script) => Checkpoint().Matches(script).Select(match => match.Groups[1].Value);

    [GeneratedRegex("\\bCheckpoint\\(\\s*\"[^\"]*\"\\s*,\\s*\"([^\"]+)\"\\s*\\)")]
    private static partial Regex Checkpoint();
}
