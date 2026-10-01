namespace TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;

public static class ChoicePropLocator
{
    public static bool IsPropFile(string name) => name.ToLowerInvariant().EndsWith(".prop", StringComparison.Ordinal);

    public static ReadOnlyMemory<byte> Find(OrderedDictionary<string, ReadOnlyMemory<byte>> files, TextWriter output)
    {
        foreach (var (name, data) in files)
        {
            if (name.ToLowerInvariant() != "choice.prop")
                continue;

            output.WriteLine($"  Found choice.prop: {data.Length} bytes");
            if (!data.IsEmpty)
                return data;

            break;
        }

        foreach (var name in files.Keys.Order(StringComparer.Ordinal))
        {
            if (!name.ToLowerInvariant().Contains("choice") || !IsPropFile(name))
                continue;

            output.WriteLine($"  Found {name}: {files[name].Length} bytes");
            return files[name];
        }

        return default;
    }
}
