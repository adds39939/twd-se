using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Handlers;

public class S1_400DaysHandler(IS1ResumePoint resume, IS1Inventory inventory, IS1SaveFactory saves, IS1CheckpointRefresher checkpoints, ISaveBundleSerializer serializer)
    : S1Handler(resume, inventory, saves, checkpoints, serializer)
{
    private static readonly string[] SeasonsInSave = [S1ChoiceCatalog.ExtraEpisodeSeasonKey];

    public override string SeasonKey => S1ChoiceCatalog.ExtraEpisodeSeasonKey;
    public override string Name => "Season 1: 400 Days";
    public override string ShortName => "400D";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "400 Days", 6),
    ];

    public override IReadOnlyList<string> IncludedSeasonKeys => SeasonsInSave;

    public override IReadOnlyList<ChoicePreset> Presets => [];

    public override string GetEpisodeId(int episode) => S1SlotFiles.EpisodeId(S1ResumePoint.ExtraEpisode);
}
