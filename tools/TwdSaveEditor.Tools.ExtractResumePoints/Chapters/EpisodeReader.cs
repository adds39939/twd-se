using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Seasons;
using TwdSaveEditor.Tools.ExtractResumePoints.Dialogs;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Chapters;

public sealed partial class EpisodeReader(string dataDirectory, GameSeason season, DialogLoader loader, MetaReader meta, IReadOnlyDictionary<string, string> constants, IReadOnlyList<DecisionNodes> decisions)
{
    public const string DebugMenuScript = "Episode.lua";
    public const string OpeningScript = "PreviouslyOn";

    private const string SceneExtension = ".scene";

    public EpisodeResume? Read(int episode)
    {
        var (scripts, files) = EpisodeFiles.For(dataDirectory, season, episode);
        var menuPath = Path.Combine(scripts, DebugMenuScript);
        if (!File.Exists(menuPath) || !Directory.Exists(files))
        {
            return null;
        }

        var index = new DialogIndex(loader);
        foreach (var dialog in Directory.EnumerateFiles(files, "*.dlog").Order())
        {
            index.Scan(dialog);
        }

        var sceneScripts = Directory.EnumerateFiles(scripts, "*.lua").Order().Select(SceneScriptReader.Read).OfType<SceneScript>().ToList();
        var scenes = new SceneMap(sceneScripts, files);
        var developerOnly = sceneScripts.Where(script => SceneScriptReader.SetupIsDeveloperOnly(script.Text))
            .ToDictionary(script => script.Script, script => script.Text, StringComparer.OrdinalIgnoreCase);
        var chapters = index.Marks
            .DistinctBy(mark => mark.ChapterId)
            .OrderBy(mark => Order(mark.ChapterId))
            .Select(mark => new GameChapter(mark.ChapterId, mark.Dialog, scenes.ScriptsFor(mark.Dialog)))
            .ToList();

        var present = Directory.EnumerateFiles(files, "*" + SceneExtension)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = present.Count == 0
            ? []
            : sceneScripts.Where(script => !present.Contains(script.Scene))
                .Select(script => script.Script)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = DebugMenuReader.Read(File.ReadAllText(menuPath), constants)
            .Where(entry => !missing.Contains(entry.Script))
            .Where(entry => !developerOnly.TryGetValue(entry.Script, out var text) || !entry.Flags.Any(flag => SceneScriptReader.Reads(text, flag.Key)))
            .ToList();
        var entryReader = season.LoadedSceneRunsCheckpoint
            ? new SceneEntryReader(meta, Path.Combine(dataDirectory, "extracted", season.ProjectArchive), files, constants)
            : null;
        var byScript = sceneScripts.ToDictionary(script => script.Script, StringComparer.OrdinalIgnoreCase);
        if (entryReader != null)
        {
            entries = entries.Where(entry => byScript.TryGetValue(entry.Script, out var script) && entryReader.For(script, entry.Flags) != null).ToList();
        }

        var aligned = ChapterAligner.Align(entries, chapters);
        var made = decisions.Where(decision => decision.Episode == episode)
            .ToDictionary(decision => decision.ChoiceKey, decision => Scripts(decision, index, scenes));

        var points = new List<ResumePoint>();
        for (var position = 0; position < entries.Count; position++)
        {
            var entry = entries[position];
            var decided = made
                .Where(decision => decision.Value.Count > 0 && decision.Value.All(script => LastVisit(entries, script) < position))
                .Select(decision => decision.Key)
                .ToList();

            points.Add(new ResumePoint(
                UniqueId(points, entry.Title),
                entry.Title,
                entry.Group,
                entry.Script,
                aligned[position],
                position == 0,
                entry.Flags,
                decided,
                entryReader != null && byScript.TryGetValue(entry.Script, out var sceneScript) ? entryReader.For(sceneScript, entry.Flags) : null));
        }

        return new EpisodeResume(episode, chapters, points, [.. made.Where(decision => decision.Value.Count == 0).Select(decision => decision.Key)]);
    }

    private static List<string> Scripts(DecisionNodes decision, DialogIndex index, SceneMap scenes) =>
    [
        .. decision.Nodes.Select(index.Owner).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(scenes.ScriptsFor).Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    private static int LastVisit(IReadOnlyList<MenuEntry> entries, string script)
    {
        for (var position = entries.Count - 1; position >= 0; position--)
        {
            if (entries[position].Script.Equals(script, StringComparison.OrdinalIgnoreCase))
            {
                return position;
            }
        }

        return int.MaxValue;
    }

    private static string UniqueId(List<ResumePoint> points, string title)
    {
        var id = NonWord().Replace(title, string.Empty);
        return points.Any(point => point.Id == id) ? id + (points.Count + 1) : id;
    }

    private static (int Number, string Suffix) Order(string chapterId)
    {
        var match = ChapterNumber().Match(chapterId);
        return match.Success ? (int.Parse(match.Groups[1].Value), match.Groups[2].Value) : (int.MaxValue, chapterId);
    }

    [GeneratedRegex("chapter(\\d+)(\\D*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterNumber();

    [GeneratedRegex("\\W")]
    private static partial Regex NonWord();
}
