using System.Text.Json;
using TwdSaveEditor.Tools.Common.Configuration;

namespace TwdSaveEditor.Tools.Common.Meta;

public sealed class ClassLayouts
{
    private readonly Dictionary<string, List<ClassLayout>> _layouts = new(StringComparer.OrdinalIgnoreCase);

    public static ClassLayouts LoadDefault()
    {
        var layouts = new ClassLayouts();
        var path = Path.Combine(ToolPaths.ToolsDirectory, "data", "names", "classes.json");
        if (File.Exists(path))
        {
            layouts.Load(path);
        }

        return layouts;
    }

    public void Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var members = new List<ClassMember>();
            if (entry.TryGetProperty("members", out var list))
            {
                foreach (var member in list.EnumerateArray())
                {
                    members.Add(new ClassMember(
                        member.GetProperty("name").GetString()!,
                        TypeName.Normalize(member.GetProperty("type").GetString()!),
                        member.GetProperty("flags").GetInt32()));
                }
            }

            var type = TypeName.Normalize(entry.GetProperty("type").GetString()!);
            if (!_layouts.TryGetValue(type, out var versions))
            {
                _layouts[type] = versions = [];
            }

            versions.Add(new ClassLayout(type, entry.GetProperty("crc32").GetUInt32(), members));
        }
    }

    public IEnumerable<string> TypeNames => _layouts.Keys;

    public ClassLayout? Find(string type, uint? version)
    {
        if (!_layouts.TryGetValue(type, out var versions))
        {
            return null;
        }

        return versions.FirstOrDefault(layout => layout.Version == version) ?? versions[^1];
    }
}
