using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Chapters;

public sealed partial class SceneEntryReader
{
    private const string DialogKey = "Scene - Dialog";
    private const string NodeKey = "Scene - Dialog Node";
    private const string SceneDefaults = "scene.prop";
    private const string ActConstant = "kAct";
    private const string PropertyExtension = ".prop";

    private readonly MetaReader _meta;
    private readonly string _episodeDirectory;
    private readonly string? _actKey;
    private readonly string? _defaultNode;
    private readonly Dictionary<ulong, string> _dialogs;

    public SceneEntryReader(MetaReader meta, string projectDirectory, string episodeDirectory, IReadOnlyDictionary<string, string> constants)
    {
        _meta = meta;
        _episodeDirectory = episodeDirectory;
        _actKey = constants.GetValueOrDefault(ActConstant);
        _dialogs = Directory.EnumerateFiles(episodeDirectory, "*.dlog")
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToDictionary(TelltaleCrc64.Compute, name => name);

        var defaults = Path.Combine(projectDirectory, SceneDefaults);
        _defaultNode = File.Exists(defaults) ? (Read(defaults)?.Find(NodeKey) as MetaScalar)?.Text : null;
    }

    public SceneEntry? For(SceneScript script, IReadOnlyList<Flag> flags)
    {
        var path = Path.Combine(_episodeDirectory, script.Scene + PropertyExtension);
        if (!File.Exists(path) || Read(path) is not { } properties)
            return null;

        if (properties.Find(DialogKey) is not MetaSymbol { IsHandle: true } handle || !_dialogs.TryGetValue(handle.Hash, out var dialog))
            return null;

        var node = (properties.Find(NodeKey) as MetaScalar)?.Text ?? _defaultNode;
        if (node == null)
            return null;

        if (_actKey != null && flags.FirstOrDefault(flag => flag.Key == _actKey)?.Value is JsonValue value && value.TryGetValue<int>(out var act))
        {
            var variant = ActSuffix().Replace(dialog, $"_act{act}");
            if (_dialogs.Values.FirstOrDefault(name => name.Equals(variant, StringComparison.OrdinalIgnoreCase)) is { } found)
                dialog = found;
        }

        foreach (Match match in NodeForFlag().Matches(script.Text))
        {
            if (flags.Any(flag => flag.Key == match.Groups["key"].Value && flag.Value is JsonValue set && set.TryGetValue<bool>(out var on) && on))
                node = match.Groups["node"].Value;
        }

        return new SceneEntry(dialog, node);
    }

    private MetaPropertySet? Read(string path) => _meta.ReadPropertySet(File.ReadAllBytes(path));

    [GeneratedRegex("_act\\d+(?=\\.dlog$)", RegexOptions.IgnoreCase)]
    private static partial Regex ActSuffix();

    [GeneratedRegex("if LogicGet\\(\"(?<key>[^\"]+)\"\\) then\\s+Game_SetSceneDialogNode\\(\"(?<node>[^\"]+)\"\\)")]
    private static partial Regex NodeForFlag();
}
