using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.UI.Components;

public partial class ResumePointEditor
{
    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private ISeasonHandler? _season;

    protected override void OnParametersSet()
    {
        _season = Slot?.DetectedSeasonKey != null ? Registry.Get(Slot.DetectedSeasonKey) : null;
    }
}
