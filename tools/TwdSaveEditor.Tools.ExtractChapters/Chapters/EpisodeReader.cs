using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.ExtractChapters.Dialogs;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scenes;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Chapters;

public sealed class EpisodeReader(string dataDirectory, DialogLoader loader)
{
    public const string DebugMenuScript = "WDEpisode.lua";

    public EpisodeChapters? Read(int episode)
    {
        var archive = $"WDC_pc_WalkingDead{episode}_data";
        var scripts = Path.Combine(dataDirectory, "lua", archive);
        var files = Path.Combine(dataDirectory, "extracted", archive);
        var menuPath = Path.Combine(scripts, DebugMenuScript);
        if (!File.Exists(menuPath) || !Directory.Exists(files))
            return null;

        var dialogs = Directory.EnumerateFiles(files, "*.dlog").Order().ToList();
        var scanner = new DialogScanner(loader);
        foreach (var dialog in dialogs)
            scanner.Scan(dialog);

        var scenes = new SceneDialogReader(dialogs.Select(Path.GetFileName).OfType<string>());
        var menu = File.ReadAllText(menuPath);

        return new EpisodeChapters(
            episode,
            [.. Directory.EnumerateFiles(scripts, "*.lua").Order().Select(SceneScriptReader.Read)],
            Directory.EnumerateFiles(files, "*.scene").ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => (IReadOnlyList<string>)scenes.Read(path),
                StringComparer.OrdinalIgnoreCase),
            DebugMenuReader.Read(menu),
            DebugMenuReader.ReadToggles(menu),
            scanner.Transitions,
            scanner.Checkpoints,
            scanner.Decisions);
    }
}
