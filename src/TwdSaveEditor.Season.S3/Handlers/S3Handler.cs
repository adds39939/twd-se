using TwdSaveEditor.Season.Base.EventLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Choices;

namespace TwdSaveEditor.Season.S3.Handlers;

public class S3Handler : EventLogSeasonHandler
{
    private static readonly string[] ImportedSeasons = ["s2"];

    public override string SeasonKey => "s3";
    public override string Name => "A New Frontier (Season 3)";
    public override string ShortName => "S3";
    public override string FilePrefix => "wd3_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "Ties That Bind - Part One", 6),
        new(2, "Ties That Bind - Part Two", 6),
        new(3, "Above the Law", 6),
        new(4, "Thicker Than Water", 6),
        new(5, "From the Gallows", 6),
    ];

    public override IReadOnlyList<string> ImportsFromSeasonKeys => ImportedSeasons;

    protected override ChoiceNodeMap ChoiceNodes => S3ChoiceNodes.Map;

    public override string GetEpisodeId(int episode) => $"WalkingDead30{episode}";
}
