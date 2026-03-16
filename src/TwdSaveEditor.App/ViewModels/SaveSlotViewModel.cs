using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.App.ViewModels;

public partial class SaveSlotViewModel : ObservableObject
{
    public SaveSlot Slot { get; }

    [ObservableProperty]
    private string _displayName;

    public ObservableCollection<PropertyViewModel> Properties { get; } = [];

    public SaveSlotViewModel(SaveSlot slot, PropertyNameDb nameDb)
    {
        Slot = slot;
        _displayName = slot.FileName;

        // Show properties from both metadata and choices
        if (slot.Metadata != null)
        {
            foreach (var group in slot.Metadata.TypeGroups)
            foreach (var prop in group.Properties)
                Properties.Add(new PropertyViewModel(prop, nameDb));
        }

        if (slot.Choices != null)
        {
            foreach (var group in slot.Choices.TypeGroups)
            foreach (var prop in group.Properties)
                Properties.Add(new PropertyViewModel(prop, nameDb));
        }
    }
}
