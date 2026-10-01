using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Hashing;

namespace TwdSaveEditor.Tools.Common.Names;

public sealed partial class SymbolNames
{
    private readonly Dictionary<ulong, string> _names = [];

    public int Count => _names.Count;

    public static SymbolNames LoadDefault()
    {
        var names = new SymbolNames();
        var data = Path.Combine(ToolPaths.ToolsDirectory, "data");

        var lists = Path.Combine(data, "names");
        if (Directory.Exists(lists))
        {
            foreach (var file in Directory.EnumerateFiles(lists, "*.txt"))
                names.AddLines(file);
        }

        var scripts = Path.Combine(data, "lua");
        if (Directory.Exists(scripts))
        {
            foreach (var file in Directory.EnumerateFiles(scripts, "*.lua", SearchOption.AllDirectories))
                names.AddScript(file);
        }

        return names;
    }

    public void Add(string name)
    {
        if (name.Length > 0)
            _names.TryAdd(TelltaleCrc64.Compute(name), name);
    }

    public void AddLines(string path)
    {
        foreach (var line in File.ReadLines(path))
            Add(line.Trim());
    }

    public void AddScript(string path)
    {
        Add(Path.GetFileName(path));
        foreach (Match match in StringLiteral().Matches(File.ReadAllText(path)))
            Add(Regex.Unescape(match.Groups[1].Value));
    }

    public string? Find(ulong hash) => _names.GetValueOrDefault(hash);

    public string Describe(ulong hash) => hash == 0 ? "<empty>" : Find(hash) is { } name ? $"\"{name}\"" : $"#{hash:X16}";

    [GeneratedRegex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"")]
    private static partial Regex StringLiteral();
}
