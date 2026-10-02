using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.ExtractChapters.Dialogs;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scenes;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Chapters;

public sealed class EpisodeReader(string dataDirectory, DialogLoader loader, SymbolNames names)
{
    public const string DebugMenuScript = "WDEpisode.lua";

    public EpisodeChapters? Read(int episode)
    {
        var (scripts, files) = EpisodeFiles.For(dataDirectory, episode);
        var menuPath = Path.Combine(scripts, DebugMenuScript);
        if (!File.Exists(menuPath) || !Directory.Exists(files))
            return null;

        var menu = File.ReadAllText(menuPath);
        var dialogs = Directory.EnumerateFiles(files, "*.dlog").Order().ToList();
        var scanner = new DialogScanner(loader, DebugMenuReader.ReadScriptLoaders(menu));
        foreach (var dialog in dialogs)
            scanner.Scan(dialog);

        var scenes = new SceneDialogReader(dialogs.Select(Path.GetFileName).OfType<string>());

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
            scanner.Decisions,
            ReadStoryActions(episode, files));
    }

    private List<StoryAction> ReadStoryActions(int episode, string files)
    {
        var hub = Path.Combine(files, StoryChapters.HubDialog);
        return episode == StoryChapters.Episode && File.Exists(hub)
            ? new StoryBoardReader(names).Read(loader.Load(hub), StoryChapters.LastStoryKey)
            : [];
    }
}
