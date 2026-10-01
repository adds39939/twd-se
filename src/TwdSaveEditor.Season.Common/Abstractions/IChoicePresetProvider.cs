using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IChoicePresetProvider
{
    IReadOnlyList<ChoicePreset> Presets { get; }
}
