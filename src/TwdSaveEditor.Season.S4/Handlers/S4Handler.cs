using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S4.Accessors;

namespace TwdSaveEditor.Season.S4.Handlers;

public class S4Handler : SeasonHandlerBase, IChoicePresetProvider
{
    private static readonly string[] ImportedSeasons = ["s3"];

    public override string SeasonKey => "s4";
    public override string Name => "The Final Season (Season 4)";
    public override string ShortName => "S4";
    public override string FilePrefix => "wd4_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "Done Running", 6),
        new(2, "Suffer the Children", 6),
        new(3, "Broken Toys", 6),
        new(4, "Take Us Back", 6),
    ];

    public override IReadOnlyList<string> ImportsFromSeasonKeys => ImportedSeasons;

    public IReadOnlyList<ChoicePreset> Presets { get; } =
    [
        new("Save Louis Path",
        [
            new("follow_violet_louis", "louis"),
            new("violetlouis_saved", "louis"),
        ]),
        new("Save Violet Path",
        [
            new("follow_violet_louis", "violet"),
            new("violetlouis_saved", "violet"),
        ]),
        new("Trust AJ Path",
        [
            new("trusted_aj", "true"),
        ]),
    ];

    public override string GetEpisodeId(int episode) => $"WalkingDead40{episode}";

    public override SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankWithChoiceStats(fileName, episodeId);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.ChoiceStats != null ? new ChoiceStatsAccessor(slot) : null;

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
        var choices = GetChoicesUpTo(episode);

        if (choices.Count == 0)
            return;

        var accessor = new ChoiceStatsAccessor(slot);
        foreach (var c in choices)
            accessor.ApplyChoice(c, 0);
    }
}
