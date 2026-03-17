using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.App.ViewModels;

/// <summary>
/// ViewModel for a single game decision/choice that can be edited.
/// </summary>
public partial class ChoiceViewModel : ObservableObject
{
    private readonly ChoiceDefinition _definition;
    private readonly IChoiceAccessor _accessor;
    private bool _initialized;

    public ChoiceViewModel(ChoiceDefinition definition, IChoiceAccessor accessor)
    {
        _definition = definition;
        _accessor = accessor;

        Description = definition.Description;
        Category = definition.Category;
        Options = definition.Options.Select(o => o.Label).ToArray();

        // Detect what's currently selected — use property so source gen is happy
        SelectedIndex = accessor.DetectCurrentChoice(definition);
        _initialized = true;
    }

    public string Description { get; }
    public string Category { get; }
    public string[] Options { get; }

    [ObservableProperty]
    private int _selectedIndex;

    public string CurrentSelection => SelectedIndex >= 0 && SelectedIndex < Options.Length
        ? Options[SelectedIndex]
        : "(unknown/not set)";

    public bool IsResolved => SelectedIndex >= 0;

    partial void OnSelectedIndexChanged(int value)
    {
        if (_initialized && value >= 0 && value < _definition.Options.Length)
        {
            _accessor.ApplyChoice(_definition, value);
        }
        OnPropertyChanged(nameof(CurrentSelection));
        OnPropertyChanged(nameof(IsResolved));
    }
}
