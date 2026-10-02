using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractChapters.Model;
using TwdSaveEditor.Tools.ExtractChapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractChapters.Chapters;

public static partial class StoryChapters
{
    public const int Episode = 106;
    public const string HubScript = "ChapterSelection.lua";

    public const string HubDialog = "env_chapterSelection.dlog";
    public const string LastStoryKey = "Last Chapter";

    private const string SceneExtension = ".scene";
    private const string HubHandler = "OnChapterSelection";

    private static readonly Story[] Stories =
    [
        new(1, "Shot Dan", "1 - Shot Dan"),
        new(2, "Left Eddie", "2 - Left Eddie"),
        new(3, "Left Nate", "3 - Walked Away"),
        new(4, "Lied To Leland", "4 - Lied to Leland"),
        new(5, "Killed Stephanie", "5 - Killed Stephanie"),
    ];

    public static List<Chapter> Expand(EpisodeChapters episode, List<Chapter> chapters)
    {
        if (episode.Episode != Episode)
            return chapters;

        var hubScene = episode.Scripts.First(script => script.Name.Equals(HubScript, StringComparison.OrdinalIgnoreCase)).Scene + SceneExtension;
        List<Assignment> Finished(int count) => FinishedStories(count, episode.StoryActions, hubScene);

        var expanded = chapters.Where(chapter => chapter.Handler == HubHandler).Select(chapter => chapter with { Group = string.Empty }).ToList();
        foreach (var chapter in chapters.Where(chapter => chapter.Handler != HubHandler))
        {
            var story = StoryOf(chapter);
            if (story > 1 && StoryOf(expanded[^1]) == story - 1 && story <= Stories.Length)
                expanded.Add(Hub(story - 1, expanded[^1].Group, Finished(story - 1)));

            expanded.Add(chapter with { Assignments = [.. Finished(story - 1), .. chapter.Assignments] });
        }

        return expanded;
    }

    public static IEnumerable<JsonObject> DecisionPoints(int episode) =>
        episode != Episode ? [] : Stories.Select(story => new JsonObject
        {
            ["choiceKey"] = story.ChoiceKey,
            ["decidedFrom"] = FirstChapterAfter(story),
        });

    public static IEnumerable<JsonObject> DecisionFlags(int episode) =>
        episode != Episode ? [] : Stories.Select(story => new JsonObject
        {
            ["choiceKey"] = story.ChoiceKey,
            ["agent"] = DebugMenuReader.GameLogicAgent,
            ["key"] = story.MirrorFlag,
            ["decidedFrom"] = FirstChapterAfter(story),
            ["values"] = new JsonObject { ["true"] = true, ["false"] = false },
        });

    private static string FirstChapterAfter(Story story) =>
        story.Number < Stories.Length ? HubAfter(story.Number) : $"OnChapter{Stories.Length + 1}A";

    private static string HubAfter(int story) => $"{HubHandler}After{story}";

    private static Chapter Hub(int finished, string group, List<Assignment> assignments) => new(
        HubAfter(finished),
        finished == 1 ? "Chapter Selection (first story finished)" : $"Chapter Selection (first {finished} stories finished)",
        group,
        HubScript,
        assignments,
        false);

    private static List<Assignment> FinishedStories(int count, IReadOnlyList<StoryAction> actions, string hubScene)
    {
        if (count <= 0)
            return [];

        var assignments = Stories.Take(count)
            .Select(story => new Assignment(DebugMenuReader.GameLogicAgent, story.CompleteFlag, JsonValue.Create(true), null))
            .ToList();
        assignments.Add(new Assignment(DebugMenuReader.GameLogicAgent, LastStoryKey, JsonValue.Create(Math.Min(count, Stories.Length)), null));
        assignments.AddRange(actions.Where(action => action.Story <= count)
            .Select(action => new Assignment(action.Agent, action.Key, JsonValue.Create(action.Value), null, hubScene)));
        return assignments;
    }

    private static int StoryOf(Chapter chapter) =>
        StoryHandler().Match(chapter.Handler) is { Success: true } match ? int.Parse(match.Groups[1].Value) : 0;

    [GeneratedRegex("^OnChapter(\\d)")]
    private static partial Regex StoryHandler();
}
