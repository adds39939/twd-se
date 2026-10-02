using System.Text.RegularExpressions;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class ConstantReader
{
    public static Dictionary<string, string> Read(string directory)
    {
        var constants = new Dictionary<string, string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.lua").Order())
        {
            foreach (Match match in Constant().Matches(File.ReadAllText(path)))
                constants.TryAdd(match.Groups[1].Value, match.Groups[2].Value);
        }

        return constants;
    }

    [GeneratedRegex("^(k\\w+) = \"([^\"]*)\"\\s*$", RegexOptions.Multiline)]
    private static partial Regex Constant();
}
