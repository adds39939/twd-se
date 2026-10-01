using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Handlers;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Accessors;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S2.Accessors;

namespace TwdSaveEditor.Season.S2.Handlers;

public class S2Handler : PropChoicesSeasonHandler, IChoiceImporter
{
    private static readonly string[] ImportedSeasons = [S1ChoiceCatalog.MainSeasonKey, S1ChoiceCatalog.ExtraEpisodeSeasonKey];

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

    protected override string ChoicesFileName => BundleFileNames.Season1Choices;

    public override string GetEpisodeId(int episode) => $"WalkingDead20{episode}";

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.Choices != null ? new S2ChoiceAccessor(slot) : null;

    public bool CanImportFrom(SaveSlot source)
        => source.DetectedSeasonKey == S1ChoiceCatalog.MainSeasonKey && source.Metadata != null && source.Choices != null;

    public void ImportChoices(SaveSlot source, SaveSlot target)
    {
        if (source.Choices == null || target.Choices == null) return;

        var sourceAccessor = new S1ChoiceAccessor(source);
        var targetAccessor = new S2ChoiceAccessor(target);

        foreach (var choice in S1ChoiceCatalog.All)
        {
            if (sourceAccessor.GetChoiceValue(choice.ChoiceKey) is { } value)
                targetAccessor.SetChoiceValue(choice.ChoiceKey, value);
        }

        for (var episode = PersistentKeys.FirstEpisode; episode <= PersistentKeys.LastEpisode; episode++)
        {
            var container = Symbol.FromString(PersistentKeys.TrackerContainer(episode));
            if (source.Choices.Find(container)?.Value is RawBytesValue tracker)
                target.Choices.Set(container, tracker.TypeSymbol, new RawBytesValue([.. tracker.Data], tracker.TypeSymbol));
        }
    }
}
