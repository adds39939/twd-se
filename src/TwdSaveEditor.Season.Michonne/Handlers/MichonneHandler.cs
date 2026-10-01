using TwdSaveEditor.Season.Base.EventLog;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.Michonne.Choices;

namespace TwdSaveEditor.Season.Michonne.Handlers;

public class MichonneHandler : EventLogSeasonHandler
{
    public override string SeasonKey => "michonne";
    public override string Name => "Michonne";
    public override string ShortName => "M";
    public override string FilePrefix => "wdm_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "In Too Deep", 6),
        new(2, "Give No Shelter", 6),
        new(3, "What We Deserve", 6),
    ];

    protected override ChoiceNodeMap ChoiceNodes => MichonneChoiceNodes.Map;

    public override string GetEpisodeId(int episode) => $"Michonne10{episode}";
}
