using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.App.ViewModels;

public partial class PropertyViewModel : ObservableObject
{
    private readonly Property _property;
    private readonly PropertyNameDb _nameDb;

    public PropertyViewModel(Property property, PropertyNameDb nameDb)
    {
        _property = property;
        _nameDb = nameDb;
        _displayName = nameDb.ResolveOrHex(property.KeySymbol);
        _typeName = property.Value.TypeName;

        if (property.Value is PropertySetValue psv)
        {
            IsExpandable = true;
            foreach (var group in psv.Value.TypeGroups)
            foreach (var prop in group.Properties)
                Children.Add(new PropertyViewModel(prop, nameDb));
        }

        UpdateValueDisplay();
    }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _typeName;

    [ObservableProperty]
    private string _valueDisplay = "";

    [ObservableProperty]
    private string _editableValue = "";

    [ObservableProperty]
    private bool _isExpandable;

    [ObservableProperty]
    private bool _isExpanded;

    public bool IsEditable => _property.Value is not (PropertySetValue or RawBytesValue);
    public Property UnderlyingProperty => _property;
    public Symbol KeySymbol => _property.KeySymbol;
    public ObservableCollection<PropertyViewModel> Children { get; } = [];

    private void UpdateValueDisplay()
    {
        var display = _property.Value switch
        {
            BoolValue b => b.Value.ToString(),
            IntValue i => i.Value.ToString(),
            FloatValue f => f.Value.ToString("G"),
            StringValue s => $"\"{s.Value}\"",
            SymbolValue sym => _nameDb.ResolveOrHex(sym.Value),
            UInt64Value u => $"0x{u.Value:X16}",
            PropertySetValue ps => $"[{ps.Value.AllProperties.Count()} properties]",
            RawBytesValue raw => $"[{raw.Data.Length} bytes]",
            _ => "?"
        };

        ValueDisplay = display;
        EditableValue = _property.Value switch
        {
            BoolValue b => b.Value.ToString(),
            IntValue i => i.Value.ToString(),
            FloatValue f => f.Value.ToString("G"),
            StringValue s => s.Value,
            SymbolValue sym => $"0x{sym.Value.Value:X16}",
            UInt64Value u => $"0x{u.Value:X16}",
            _ => display
        };
    }

    public bool TryApplyEdit(string newValue)
    {
        try
        {
            switch (_property.Value)
            {
                case BoolValue b:
                    b.Value = bool.Parse(newValue);
                    break;
                case IntValue i:
                    i.Value = int.Parse(newValue);
                    break;
                case FloatValue f:
                    f.Value = float.Parse(newValue);
                    break;
                case StringValue s:
                    s.Value = newValue;
                    break;
                case SymbolValue sym:
                    sym.Value = new Symbol(Convert.ToUInt64(newValue.Replace("0x", ""), 16));
                    break;
                case UInt64Value u:
                    u.Value = Convert.ToUInt64(newValue.Replace("0x", ""), 16);
                    break;
                default:
                    return false;
            }

            UpdateValueDisplay();
            OnPropertyChanged(nameof(ValueDisplay));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
