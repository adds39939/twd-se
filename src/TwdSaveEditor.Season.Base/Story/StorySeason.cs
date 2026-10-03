using System.Reflection;
using TwdSaveEditor.Season.Base.Resources;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StorySeason
{
    public const int FirstEpisode = 1;

    private const string DecisionsSuffix = ".decisions.json";
    private const string ChaptersSuffix = ".chapters.json";
    private const string ItemsSuffix = ".items.json";

    private readonly Lazy<StoryDecisionData> _decisions;
    private readonly Lazy<StoryChapterData> _chapters;
    private readonly Lazy<StoryItemData> _items;

    public StorySeason(Assembly data)
    {
        _decisions = new(() => EmbeddedSeasonData.Load<StoryDecisionData>(data, DecisionsSuffix) ?? new StoryDecisionData([], []));
        _chapters = new(() => EmbeddedSeasonData.Load<StoryChapterData>(data, ChaptersSuffix) ?? new StoryChapterData([]));
        _items = new(() => EmbeddedSeasonData.Load<StoryItemData>(data, ItemsSuffix) ?? new StoryItemData([]));
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
}
