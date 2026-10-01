using TwdSaveEditor.Tools.Common.Hashing;

namespace TwdSaveEditor.Tools.Common.Meta;

public static class TypeName
{
    private static readonly string[] Qualifiers = ["class ", "struct ", "enum ", "std::", " "];

    public static string Normalize(string name)
    {
        foreach (var qualifier in Qualifiers)
            name = name.Replace(qualifier, string.Empty, StringComparison.Ordinal);

        return name;
    }

    public static ulong Hash(string name) => TelltaleCrc64.Compute(Normalize(name));

    public static (string Template, List<string> Arguments) Split(string name)
    {
        var open = name.IndexOf('<');
        if (open < 0 || !name.EndsWith('>'))
            return (name, []);

        var arguments = new List<string>();
        var depth = 0;
        var start = open + 1;
        for (var i = open + 1; i < name.Length - 1; i++)
        {
            switch (name[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    arguments.Add(name[start..i]);
                    start = i + 1;
                    break;
            }
        }

        arguments.Add(name[start..^1]);
        return (name[..open], arguments);
    }
}
