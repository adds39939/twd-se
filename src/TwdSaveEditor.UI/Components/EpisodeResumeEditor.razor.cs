using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class EpisodeResumeEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Parameter, EditorRequired] public SaveSlot Slot { get; set; } = default!;

    [Parameter, EditorRequired] public IResumePointHandler Handler { get; set; } = default!;

    private ResumeState _state = new(1, null, null);
    private int _episode = 1;
    private string _chapter = string.Empty;
    private IReadOnlyList<ChapterInfo> _chapters = [];
    private SaveSlot? _loadedSlot;

    protected override void OnParametersSet()
    {
        _state = Handler.GetResumeState(Slot);
        if (ReferenceEquals(_loadedSlot, Slot))
        {
            return;
        }

        _loadedSlot = Slot;
        SelectEpisode(_state.Episode);
    }

    private void OnEpisodeChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var episode))
        {
            return;
        }

        SelectEpisode(episode);
        Apply();
    }

    private void OnChapterChanged(ChangeEventArgs e)
    {
        _chapter = e.Value?.ToString() ?? string.Empty;
        Apply();
    }

    private void SelectEpisode(int episode)
    {
        _episode = episode;
        _chapters = Handler.GetChapters(episode);
        _chapter = CurrentChapter() ?? _chapters.FirstOrDefault()?.Id ?? string.Empty;
    }

    private string? CurrentChapter() =>
        _episode == _state.Episode && _state.Checkpoint != null
            ? _chapters.FirstOrDefault(chapter => chapter.Title == _state.Checkpoint)?.Id
            : _episode == _state.Episode && _state.StartsFromBeginning
                ? _chapters.FirstOrDefault()?.Id ?? string.Empty
                : null;

    private string ChapterLabel(ChapterInfo chapter) =>
        ReferenceEquals(chapter, _chapters[0]) ? $"{chapter.Title} (start of the episode)" : chapter.Title;

    private string EpisodeLabel(int number)
    {
        var episode = Handler.ResumeEpisodes.FirstOrDefault(e => e.Number == number);
        return episode == null ? $"Episode {number}" : $"Episode {episode.Number}: {episode.Title}";
    }

    private void Apply()
    {
        if (_chapter == CurrentChapter())
        {
            return;
        }

        if (_chapter.Length == 0)
        {
            Handler.RestartFromEpisode(Slot, _episode);
        }
        else
        {
            Handler.RestartFromChapter(Slot, _episode, _chapter);
        }

        _state = Handler.GetResumeState(Slot);
        Editor.MarkModified();
    }
}
