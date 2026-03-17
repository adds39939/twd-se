using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Components;

public partial class ResumePointEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private int _episode = 1;
    private int _chapter = 1;
    private int _maxChapters = 7;
    private bool _gameComplete;

    protected override void OnParametersSet()
    {
        if (Slot?.Metadata == null) return;

        var accessor = new SaveAccessor(Slot.Choices, Slot.Metadata);

        _episode = accessor.GetMetadataInt(ResumePoint.Keys.EpisodeNumber) ??
                   accessor.GetMetadataInt(ResumePoint.Keys.CurrentEpisode) ?? 1;
        _chapter = accessor.GetMetadataInt(ResumePoint.Keys.ChapterNumber) ??
                   accessor.GetMetadataInt(ResumePoint.Keys.CurrentChapter) ?? 1;

        var seasonInfo = SeasonInfo.FindSeason(Slot.DetectedSeasonKey ?? "s1");
        if (seasonInfo != null)
        {
            var ep = seasonInfo.Episodes.FirstOrDefault(e => e.Number == _episode);
            _maxChapters = ep?.ChapterCount ?? 7;
        }

        // Game complete is typically stored as a bool in metadata
        var gcProp = Slot.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol == Symbol.FromString(ResumePoint.Keys.GameComplete));
        _gameComplete = gcProp?.Value is BoolValue bv && bv.Value;
    }

    private void OnEpisodeChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var ep)) return;
        _episode = ep;

        var seasonInfo = SeasonInfo.FindSeason(Slot?.DetectedSeasonKey ?? "s1");
        var epInfo = seasonInfo?.Episodes.FirstOrDefault(x => x.Number == ep);
        _maxChapters = epInfo?.ChapterCount ?? 7;
        if (_chapter > _maxChapters) _chapter = 1;

        UpdateMetadata();
    }

    private void OnChapterChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var ch)) return;
        _chapter = ch;
        UpdateMetadata();
    }

    private void OnGameCompleteChanged(ChangeEventArgs e)
    {
        _gameComplete = (bool)(e.Value ?? false);
        UpdateMetadata();
    }

    private void UpdateMetadata()
    {
        if (Slot?.Metadata == null) return;

        SetMetadataInt(ResumePoint.Keys.EpisodeNumber, _episode);
        SetMetadataInt(ResumePoint.Keys.ChapterNumber, _chapter);

        var gcSymbol = Symbol.FromString(ResumePoint.Keys.GameComplete);
        var gcProp = Slot.Metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == gcSymbol);
        if (gcProp?.Value is BoolValue bv)
            bv.Value = _gameComplete;

        Editor.MarkModified();
    }

    private void SetMetadataInt(string keyName, int value)
    {
        if (Slot?.Metadata == null) return;
        var symbol = Symbol.FromString(keyName);
        var prop = Slot.Metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        if (prop?.Value is IntValue iv)
            iv.Value = value;
    }

}
