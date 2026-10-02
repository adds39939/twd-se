using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.ExtractChapters.Chapters;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scenes;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Items;

public sealed class EpisodeItemReader(string dataDirectory, DialogLoader loader)
{
    private const int EpisodeBase = 100;

    public EpisodeItems Read(EpisodeChapters episode, JsonObject plan, IReadOnlyList<Decision> decisions)
    {
        var files = EpisodeFiles.For(dataDirectory, episode.Episode);
        var menu = Path.Combine(files.Scripts, EpisodeReader.DebugMenuScript);
        var items = ItemRegistryReader.Read(File.ReadAllText(menu));
        var keys = items.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);

        var dialogs = Directory.EnumerateFiles(files.Extracted, "*.dlog").Order().ToList();
        var scenes = new SceneDialogReader(dialogs.Select(Path.GetFileName).OfType<string>());
        var references = Directory.EnumerateFiles(files.Extracted, "*.scene").ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            scenes.ReadEveryReference,
            StringComparer.OrdinalIgnoreCase);

        var owners = new DialogOwners(episode);
        var reader = new ItemChangeReader(items);
        foreach (var path in dialogs)
        {
            var name = Path.GetFileName(path);
            var referring = episode.Scripts
                .Where(script => script.Scene != null && references.GetValueOrDefault(script.Scene)?.Contains(name, StringComparer.OrdinalIgnoreCase) == true)
                .Select(script => script.Name);
            var known = owners.Of(name).Union(referring, StringComparer.OrdinalIgnoreCase).ToList();
            reader.ReadDialog(loader.Load(path), known.Count > 0 ? known : [.. owners.NamedLike(name)]);
        }

        foreach (var path in Directory.EnumerateFiles(files.Scripts, "*.lua").Order().Where(path => path != menu))
            reader.ReadScript(Path.GetFileName(path), File.ReadAllText(path));

        var points = plan["chapters"]!.AsArray().Select(chapter => new TimelinePoint(
            chapter!["id"]!.GetValue<string>(),
            chapter["script"]!.GetValue<string>(),
            null,
            [.. chapter["flags"]!.AsArray().Where(flag => keys.Contains(flag!["key"]!.GetValue<string>()) && Held(flag["value"])).Select(flag => flag!["key"]!.GetValue<string>())])).ToList();

        var defined = items.Select(item => item with
        {
            MaxCount = Math.Max(reader.Increases.GetValueOrDefault(item.Key), 1),
            ChoiceKey = decisions.FirstOrDefault(decision => decision.Value.Equals(item.Key, StringComparison.OrdinalIgnoreCase))?.Key,
        }).ToList();

        var chosen = defined.Where(item => item.ChoiceKey != null).Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var timeline = new ItemTimeline(points, [.. reader.Changes.Where(change => !change.Removes || !chosen.Contains(change.Item))], strict: true);
        return new EpisodeItems(
            episode.Episode - EpisodeBase,
            defined,
            [.. points.Select((point, position) => new ChapterItems(point.Id, [.. items.Select(item => item.Key).Where(item => timeline.Held(item, position, false))]))]);
    }

    private static bool Held(JsonNode? value) => value is JsonValue scalar && (scalar.TryGetValue<bool>(out var flag) ? flag : scalar.TryGetValue<int>(out var count) && count > 0);
}
