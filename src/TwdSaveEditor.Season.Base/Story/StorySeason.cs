using System.Reflection;
using System.Text.Json;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StorySeason
{
    public const int FirstEpisode = 1;

    private const string DecisionsSuffix = ".decisions.json";
    private const string ChaptersSuffix = ".chapters.json";
    private const string ItemsSuffix = ".items.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Lazy<StoryDecisionData> _decisions;
    private readonly Lazy<StoryChapterData> _chapters;
    private readonly Lazy<StoryItemData> _items;

    public StorySeason(Assembly data)
    {
        _decisions = new(() => Load<StoryDecisionData>(data, DecisionsSuffix) ?? new StoryDecisionData([], []));
        _chapters = new(() => Load<StoryChapterData>(data, ChaptersSuffix) ?? new StoryChapterData([]));
        _items = new(() => Load<StoryItemData>(data, ItemsSuffix) ?? new StoryItemData([]));
    }

    public required int LastEpisode { get; init; }

    public required string ProjectPrefix { get; init; }

    public required string SlotMetadataParent { get; init; }

    public required string SaveMetadataParent { get; init; }

    public required IReadOnlyList<string> SharedResourceSets { get; init; }

    public required string DateFormat { get; init; }

    public bool PreviousGameData { get; init; }

    public bool ChapterSaves { get; init; }

    public StoryNumberKind FinishedEpisodeKind { get; init; }

    public string? PreviousSeasonKey { get; init; }

    public bool GameLogicVisible { get; init; }

    public ulong ScriptProperties { get; init; } = StoryFiles.ScriptProperties;

    public ulong SaveLoadProperties { get; init; } = StoryFiles.SaveLoadProperties;

    public IReadOnlyList<StoryDecision> Decisions => _decisions.Value.Decisions;

    public IReadOnlyList<StoryLogicKey> LogicKeys => _decisions.Value.LogicKeys;

    public IReadOnlyList<StoryEpisodeChapters> Chapters => _chapters.Value.Episodes;

    public StoryDecision? FindDecision(string choiceKey) =>
        Decisions.FirstOrDefault(decision => decision.ChoiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));

    public StoryEpisodeChapters? ChaptersOf(int episode) => Chapters.FirstOrDefault(entry => entry.Episode == episode);

    public StoryEpisodeItems? ItemsOf(int episode) => _items.Value.Episodes.FirstOrDefault(entry => entry.Episode == episode);

    public string ProjectName(int episode) => ProjectPrefix + episode;

    public int EpisodeNumber(string? project) =>
        project != null && project.StartsWith(ProjectPrefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(project[ProjectPrefix.Length..], out var episode)
            ? episode
            : FirstEpisode;

    private static T? Load<T>(Assembly assembly, string suffix)
    {
        var name = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }
}
