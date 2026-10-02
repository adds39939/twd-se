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

    private static readonly HashSet<string> Excluded = new(IgnoreCase) { RecapScript, "NextTimeOn.lua" };

    private const string TrueValue = "true";
    private const string FalseValue = "false";

    private static readonly Dictionary<string, string> DecidedFrom = new()
    {
        ["1Morgue - Cut Off Arm"] = "OnElevatorShaft",
        ["3Room - Killed Campman"] = "OnMarshHouseInterior2",
    };

    private static readonly (int Episode, string ChoiceKey, string Value, string Agent, string Key, int Amount, string DecidedFrom)[] InventoryDecisions =
    [
        (105, "Surrendered Cleaver", TrueValue, DebugMenuReader.InventoryAgent, "Inventory - Cleaver", 0, "OnCampmanFight"),
    ];

    private readonly Dictionary<string, SceneScript> _scripts = episode.Scripts.ToDictionary(script => script.Name, IgnoreCase);

    private readonly DialogOwners _owners = new(episode);

    public List<string> Skipped { get; } = [];

    public JsonObject Plan()
    {
        var recap = _scripts.GetValueOrDefault(RecapScript);
        var candidates = StoryChapters.Expand(episode,
            episode.Chapters.Where(chapter => chapter.Script != null && !Excluded.Contains(chapter.Script)).ToList());
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
                    ["scene"] = assignment.Scene,
                    ["key"] = assignment.Key,
                    ["value"] = assignment.Value?.DeepClone(),
                })]),
            })]),
            ["decisionFlags"] = new JsonArray([.. DecisionFlags(keys, kept), .. StoryChapters.DecisionFlags(episode.Episode)]),
            ["decisionPoints"] = new JsonArray([.. StoryChapters.DecisionPoints(episode.Episode)]),
        };
    }

    private JsonObject? Entry(Chapter chapter, List<Chapter> earlier, string? firstScene, bool hasRecap, out bool supported)
    {
        supported = true;
        var first = IgnoreCase.Equals(chapter.Script, firstScene);
        if (first && !earlier.Any(candidate => IgnoreCase.Equals(candidate.Script, firstScene)) && chapter.Assignments.Count == 0)
            return null;

        var transitions = episode.Transitions.Where(candidate => IgnoreCase.Equals(candidate.Target, chapter.Script)).ToList();
        var previous = earlier.LastOrDefault(candidate => !IgnoreCase.Equals(candidate.Script, chapter.Script) && SceneOf(candidate.Script) != null)?.Script;
        var owned = transitions
            .SelectMany(transition => _owners.Of(transition.Dialog).Select(owner => (Transition: transition, Host: owner)))
            .Where(candidate => !IgnoreCase.Equals(candidate.Host, chapter.Script))
            .ToList();

        var chosen = owned.Where(candidate => IgnoreCase.Equals(candidate.Host, previous)).Take(1)
            .Concat(owned.Take(1))
            .Concat(transitions.Take(previous == null ? 0 : 1).Select(transition => (Transition: transition, Host: previous!)))
            .ToList();

        if (chosen.Count > 0)
        {
            var (transition, host) = chosen[0];
            return new JsonObject
            {
                ["script"] = host,
                ["scene"] = SceneOf(host) + SceneExtension,
                ["dialog"] = transition.Dialog,
                ["node"] = NodePrefix + transition.Node.ToString(CultureInfo.InvariantCulture),
            };
        }

        if (first && hasRecap)
            return new JsonObject { ["script"] = RecapScript };

        supported = false;
        return null;
    }

    private IEnumerable<JsonObject> DecisionFlags(List<string> keys, List<Chapter> chapters)
    {
        foreach (var toggle in episode.Episode == StoryChapters.Episode ? [] : episode.Toggles)
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
                ["values"] = new JsonObject { [TrueValue] = true, [FalseValue] = false },
            };
        }

        foreach (var decision in InventoryDecisions.Where(decision => decision.Episode == episode.Episode))
        {
            if (!keys.Contains(decision.ChoiceKey, IgnoreCase) || chapters.All(candidate => candidate.Handler != decision.DecidedFrom))
                throw new InvalidDataException($"The inventory decision '{decision.ChoiceKey}' does not match the episode's data.");

            yield return new JsonObject
            {
                ["choiceKey"] = decision.ChoiceKey,
                ["agent"] = decision.Agent,
                ["key"] = decision.Key,
                ["decidedFrom"] = decision.DecidedFrom,
                ["values"] = new JsonObject { [decision.Value] = decision.Amount },
            };
        }
    }

    private string? SceneOf(string? script) => script == null ? null : _scripts.GetValueOrDefault(script)?.Scene;
}
