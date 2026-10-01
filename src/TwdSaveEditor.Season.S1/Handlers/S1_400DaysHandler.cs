using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S1.Handlers;

public class S1_400DaysHandler : S1Handler
{
    private static readonly string[] SeasonsInSave = ["s1_400days"];

    public override string SeasonKey => "s1_400days";
    public override string Name => "Season 1: 400 Days";
    public override string ShortName => "400D";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "400 Days", 6),
    ];

    public override IReadOnlyList<string> IncludedSeasonKeys => SeasonsInSave;

    public override string GetEpisodeId(int episode) => "WalkingDead104";
}
