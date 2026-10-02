using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Base.Story;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Michonne.Story;

namespace TwdSaveEditor.Season.Michonne.Handlers;

public class MichonneHandler : StorySeasonHandler
{
    public override string SeasonKey => "michonne";
    public override string Name => "Michonne";
    public override string ShortName => "M";
    public override string FilePrefix => "wdm_";

    public override StorySeason Season => MichonneStory.Season;

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "In Too Deep", 6),
        new(2, "Give No Shelter", 6),
        new(3, "What We Deserve", 6),
    ];

    public override IReadOnlyList<string> ResumeNotes { get; } =
    [
        "Michonne works out its decisions from a log of what was played. Restarting an episode removes its saves and cuts the log back to where that episode began; earlier episodes are marked as finished so the game does not offer to randomise their decisions.",
        "From a later chapter, a small checkpoint is written that opens the scene the way the developers' chapter menu does. Decisions made in scenes before the chapter are kept; the others are cleared so they can be made again.",
        "The game lists a slot without any save as empty, so a slot that would be left with none gets a checkpoint that starts the episode.",
    ];
}
