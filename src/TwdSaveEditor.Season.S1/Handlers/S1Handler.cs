using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S1.Handlers;

public class S1Handler : PropChoicesSeasonHandler
{
    private static readonly string[] SeasonsInSave = ["s1", "s1_400days"];

    public override string SeasonKey => "s1";
    public override string Name => "Season 1";
    public override string ShortName => "S1";
    public override string FilePrefix => "wd1_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "A New Day", 7),
        new(2, "Starved for Help", 7),
        new(3, "Long Road Ahead", 7),
        new(4, "Around Every Corner", 7),
        new(5, "No Time Left", 7),
    ];

    public override IReadOnlyList<string> IncludedSeasonKeys => SeasonsInSave;

    public override string GetEpisodeId(int episode) => $"WalkingDead10{episode}";
}
