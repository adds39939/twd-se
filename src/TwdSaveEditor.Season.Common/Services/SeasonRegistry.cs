using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.Season.Common.Services;

public class SeasonRegistry : ISeasonRegistry
{
    private readonly Dictionary<string, ISeasonHandler> _handlers;
    private readonly List<ISeasonHandler> _all;

    public SeasonRegistry(IEnumerable<ISeasonHandler> handlers)
    {
        _all = handlers.ToList();
        _handlers = new Dictionary<string, ISeasonHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in _all)
        {
            _handlers[handler.SeasonKey] = handler;
        }
    }

    public IReadOnlyList<ISeasonHandler> All => _all;

    public ISeasonHandler? Get(string seasonKey)
        => _handlers.GetValueOrDefault(seasonKey);

    public ISeasonHandler? DetectFromFileName(string fileName)
        => _all.FirstOrDefault(h => h.CanHandle(fileName));
}
