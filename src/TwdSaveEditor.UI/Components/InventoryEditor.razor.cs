using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class InventoryEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private ISeasonHandler? _season;
    private IInventoryHandler? _handler;
    private InventoryState? _state;

    protected override void OnParametersSet()
    {
        _season = Slot?.DetectedSeasonKey != null ? Registry.Get(Slot.DetectedSeasonKey) : null;
        _handler = _season as IInventoryHandler;
        _state = Slot != null ? _handler?.GetInventory(Slot) : null;
    }

    private bool IsHeld(InventoryItem item) => _state!.Held.Contains(item.Id);

    private string EpisodeLabel(int number)
    {
        var episode = _season?.Episodes.FirstOrDefault(e => e.Number == number);
        return episode == null ? $"Episode {number}" : $"Episode {episode.Number}: {episode.Title}";
    }

    private void Toggle(InventoryItem item, bool held) =>
        Apply(held ? [.. _state!.Held, item.Id] : [.. _state!.Held.Where(id => id != item.Id)]);

    private void AddCarried() => Apply([.. _state!.Held, .. _handler!.GetCarriedItems(Slot!)]);

    private void RemoveAll() => Apply([]);

    private void Apply(IReadOnlyList<string> items)
    {
        _handler!.SetInventory(Slot!, [.. items.Distinct()]);
        _state = _handler.GetInventory(Slot!);
        Editor.MarkModified();
    }
}
