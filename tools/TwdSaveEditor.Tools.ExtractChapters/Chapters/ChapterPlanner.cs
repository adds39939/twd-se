using System.Globalization;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Chapters;

public sealed class ChapterPlanner(EpisodeChapters episode)
{
    public const string RecapScript = "PreviouslyOn.lua";
    public const string NodePrefix = "dlg_id: ";

    private const int EpisodeBase = 100;
    private const string SceneExtension = ".scene";

    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> Excluded = new(IgnoreCase) { RecapScript, "NextTimeOn.lua", "ChapterSelection.lua" };

    private static readonly Dictionary<string, string> DecidedFrom = new()
    {
        ["1Morgue - Cut Off Arm"] = "OnElevatorShaft",
        ["3Room - Killed Campman"] = "OnMarshHouseInterior2",
    };

    private readonly Dictionary<string, SceneScript> _scripts = episode.Scripts.ToDictionary(script => script.Name, IgnoreCase);

    public List<string> Skipped { get; } = [];

    public JsonObject Plan()
    {
        var recap = _scripts.GetValueOrDefault(RecapScript);
        var candidates = episode.Chapters.Where(chapter => chapter.Script != null && !Excluded.Contains(chapter.Script)).ToList();
        var firstScene = recap?.LoadedScripts.FirstOrDefault() ?? candidates.FirstOrDefault()?.Script;

        var kept = new List<Chapter>();
        var entries = new List<JsonObject?>();
        foreach (var chapter in candidates)
        {
            var entry = Entry(chapter, kept, firstScene, recap != null, out var supported);
            if (!supported)
            {
                Skipped.Add($"{episode.Episode} {chapter.Title} ({chapter.Script})");
                continue;
            }

            kept.Add(chapter);
            entries.Add(entry);
        }

        var keys = episode.Decisions.Select(decision => decision.Key).Distinct(IgnoreCase).ToList();
        return new JsonObject
        {
            ["episode"] = episode.Episode - EpisodeBase,
            ["chapters"] = new JsonArray([.. kept.Select((chapter, index) => new JsonObject
            {
                ["id"] = chapter.Handler,
                ["title"] = chapter.Title,
                ["group"] = chapter.Group,
                ["script"] = chapter.Script,
                ["entry"] = entries[index],
                ["flags"] = new JsonArray([.. chapter.Assignments.Where(assignment => assignment.Unresolved == null).Select(assignment => new JsonObject
                {
                    ["agent"] = assignment.Agent,
                    ["key"] = assignment.Key,
                    ["value"] = assignment.Value?.DeepClone(),
                })]),
            })]),
            ["decisionFlags"] = new JsonArray([.. DecisionFlags(keys, kept)]),
        };
    }

    private JsonObject? Entry(Chapter chapter, List<Chapter> earlier, string? firstScene, bool hasRecap, out bool supported)
    {
        supported = true;
        var transition = episode.Transitions.FirstOrDefault(candidate => IgnoreCase.Equals(candidate.Target, chapter.Script));
        var host = earlier.LastOrDefault(candidate => !IgnoreCase.Equals(candidate.Script, chapter.Script) && SceneOf(candidate.Script) != null);
        if (transition != null && host != null)
        {
            return new JsonObject
            {
                ["script"] = host.Script,
                ["scene"] = SceneOf(host.Script) + SceneExtension,
                ["dialog"] = transition.Dialog,
                ["node"] = NodePrefix + transition.Node.ToString(CultureInfo.InvariantCulture),
            };
        }

        if (IgnoreCase.Equals(chapter.Script, firstScene))
        {
            if (!earlier.Any(candidate => IgnoreCase.Equals(candidate.Script, firstScene)) && chapter.Assignments.Count == 0)
                return null;

            if (hasRecap)
                return new JsonObject { ["script"] = RecapScript };
        }

        supported = false;
        return null;
    }

    private IEnumerable<JsonObject> DecisionFlags(List<string> keys, List<Chapter> chapters)
    {
        foreach (var toggle in episode.Toggles)
        {
            var key = keys.FirstOrDefault(candidate => toggle.EndsWith(" - " + candidate, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                continue;

            if (!DecidedFrom.TryGetValue(toggle, out var chapter) || chapters.All(candidate => candidate.Handler != chapter))
                throw new InvalidDataException($"No chapter is recorded as the first one after the decision flag '{toggle}'.");

            yield return new JsonObject
            {
                ["choiceKey"] = key,
                ["agent"] = DebugMenuReader.GameLogicAgent,
                ["key"] = toggle,
                ["decidedFrom"] = chapter,
            };
        }
    }

    private string? SceneOf(string? script) => script == null ? null : _scripts.GetValueOrDefault(script)?.Scene;
}
