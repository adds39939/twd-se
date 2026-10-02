using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.Common.Seasons;
using TwdSaveEditor.Tools.ExtractResumePoints.Chapters;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Items;

public sealed class LogicItemReader(string dataDirectory, GameSeason season, DialogLoader loader)
{
    public const string Agent = "logic_inventory";

    private const int Assign = 0;

    public EpisodeItems Read(EpisodeResume episode)
    {
        var files = EpisodeFiles.For(dataDirectory, season, episode.Episode);
        var items = InventoryCalls.Registered(File.ReadAllText(Path.Combine(files.Scripts, EpisodeReader.DebugMenuScript))).ToList();
        var byKey = items.ToDictionary(item => TelltaleCrc64.Compute(item.Id), item => item.Id);
        var scenes = new SceneMap([.. Directory.EnumerateFiles(files.Scripts, "*.lua").Order().Select(SceneScriptReader.Read).OfType<SceneScript>()], files.Extracted);

        var known = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var changes = Directory.EnumerateFiles(files.Extracted, "*.dlog").Order()
            .Select(loader.Load)
            .SelectMany(dialog => Changes(dialog, scenes.ScriptsFor(dialog.Name), byKey)
                .Concat(DialogItemChanges.Read(dialog, scenes.ScriptsFor(dialog.Name), known, fullNames: true)))
            .ToList();

        var given = changes.Where(change => !change.Removes).Select(change => change.Item).ToHashSet(StringComparer.Ordinal);
        var used = items.Where(item => given.Contains(item.Id)).ToList();
        var carried = new CarriedItems(episode.Points, changes, Array.Empty<string>().ToLookup(script => script));
        return new EpisodeItems(
            episode.Episode,
            used,
            [],
            [.. Enumerable.Range(0, episode.Points.Count).Select(position => carried.At(position, used, []))]);
    }

    private static IEnumerable<ItemChange> Changes(DialogFile dialog, IReadOnlyList<string> scripts, Dictionary<ulong, string> items) =>
        dialog.Nodes.Values.OrderBy(node => node.Id)
            .Where(node => node.Rule != null)
            .SelectMany(node => node.Rule!.Actions.AllEntries.Concat(node.Rule.Otherwise.AllEntries))
            .Where(action => action.Action == Assign && action.Target.Equals(Agent, StringComparison.OrdinalIgnoreCase) && items.ContainsKey(action.Key))
            .Select(action => new ItemChange(items[action.Key], !ItemValue.Held(action.Value), scripts, null));
}
