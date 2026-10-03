using System.Globalization;
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

    private bool IsHeld(InventoryItem item) => _state!.CountOf(item.Id) > 0;

    private string EpisodeLabel(int number)
    {
        var episode = _season?.Episodes.FirstOrDefault(e => e.Number == number);
        return episode == null ? $"Episode {number}" : $"Episode {episode.Number}: {episode.Title}";
    }

    private void Toggle(InventoryItem item, bool held) => SetCount(item, held ? 1 : 0);

    private void SetCount(InventoryItem item, object? value)
    {
        if (!int.TryParse(value?.ToString(), CultureInfo.InvariantCulture, out var count))
        {
            return;
        }

        Apply([.. _state!.Held.Where(held => held.Id != item.Id), .. count > 0 ? new[] { new HeldItem(item.Id, count) } : []]);
    }

    private void AddCarried() => Apply([.. _state!.Held, .. _handler!.GetCarriedItems(Slot!)]);

    private void RemoveAll() => Apply([]);

    private void Apply(IReadOnlyList<HeldItem> items)
    {
        _handler!.SetInventory(Slot!, [.. items.GroupBy(item => item.Id).Select(group => group.MaxBy(item => item.Count)!)]);
        _state = _handler.GetInventory(Slot!);
        Editor.MarkModified(Slot!);
    }
}
