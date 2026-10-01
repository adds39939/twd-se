using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.S2.Handlers;

public class S2Handler : PropChoicesSeasonHandler, IChoiceImporter
{
    private static readonly string[] ImportedSeasons = ["s1", "s1_400days"];

    public override string SeasonKey => "s2";
    public override string Name => "Season 2";
    public override string ShortName => "S2";
    public override string FilePrefix => "wd2_";

    public override IReadOnlyList<EpisodeInfo> Episodes { get; } =
    [
        new(1, "All That Remains", 7),
        new(2, "A House Divided", 7),
        new(3, "In Harm's Way", 7),
        new(4, "Amid the Ruins", 7),
        new(5, "No Going Back", 7),
    ];

    public override IReadOnlyList<string> ImportsFromSeasonKeys => ImportedSeasons;

    protected override string ChoicesFileName => "season1.prop";

    public override string GetEpisodeId(int episode) => $"WalkingDead20{episode}";

    public bool CanImportFrom(SaveSlot source)
        => source.DetectedSeasonKey == "s1" && source.Choices != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.Choices == null || target.Choices == null) return;

        var sourceAccessor = new SaveAccessor(source.Choices);
        var targetAccessor = new SaveAccessor(target.Choices);

        foreach (var (key, value) in sourceAccessor.GetAllChoices())
        {
            targetAccessor.SetChoiceValue(key, value);
        }
    }
}
