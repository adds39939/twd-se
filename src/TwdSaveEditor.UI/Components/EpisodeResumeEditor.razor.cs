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
    private SaveSlot? _loadedSlot;

    protected override void OnParametersSet()
    {
        _state = Handler.GetResumeState(Slot);
        if (ReferenceEquals(_loadedSlot, Slot))
            return;

        _loadedSlot = Slot;
        _episode = _state.Episode;
    }

    private string EpisodeLabel(int number)
    {
        var episode = Handler.ResumeEpisodes.FirstOrDefault(e => e.Number == number);
        return episode == null ? $"Episode {number}" : $"Episode {episode.Number}: {episode.Title}";
    }

    private void Restart()
    {
        Handler.RestartFromEpisode(Slot, _episode);
        _state = Handler.GetResumeState(Slot);
        Editor.MarkModified();
    }
}
