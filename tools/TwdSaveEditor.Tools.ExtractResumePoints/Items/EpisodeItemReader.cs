using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractResumePoints.Chapters;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Items;

public sealed class EpisodeItemReader(string dataDirectory, int season, MetaReader meta, DialogLoader loader)
{
    public EpisodeItems Read(EpisodeResume episode)
    {
        var files = EpisodeFiles.For(dataDirectory, season, episode.Episode);
        var items = new ItemCatalogReader(meta, loader).Read(files.Extracted);
        var known = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        var scripts = Directory.EnumerateFiles(files.Scripts, "*.lua").Order().Select(SceneScriptReader.Read).OfType<SceneScript>().ToList();
        var scenes = new SceneMap(scripts, files.Extracted);
        var setups = scripts
            .Where(script => !script.Script.Equals(EpisodeReader.OpeningScript, StringComparison.OrdinalIgnoreCase))
            .SelectMany(script => InventoryCalls.Added(script.Text).Where(known.Contains).Select(item => (script.Script, Item: item)))
            .ToLookup(entry => entry.Script, entry => entry.Item, StringComparer.OrdinalIgnoreCase);

        var changes = Directory.EnumerateFiles(files.Extracted, "*.dlog").Order()
            .Select(loader.Load)
            .SelectMany(dialog => DialogItemChanges.Read(dialog, scenes.ScriptsFor(dialog.Name), known))
            .ToList();

        var opening = scripts.FirstOrDefault(script => script.Script.Equals(EpisodeReader.OpeningScript, StringComparison.OrdinalIgnoreCase));
        var starting = opening == null ? [] : StartingItemReader.Read(opening.Text).Where(item => known.Contains(item.Item)).ToList();
        var startingIds = starting.Select(item => item.Item).ToHashSet(StringComparer.Ordinal);

        var carried = new CarriedItems(episode.Points, changes, setups);
        return new EpisodeItems(
            episode.Episode,
            items,
            starting,
            [.. Enumerable.Range(0, episode.Points.Count).Select(position => carried.At(position, items, startingIds))]);
    }
}
