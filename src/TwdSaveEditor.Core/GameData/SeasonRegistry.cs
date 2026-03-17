namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Registry that maps season keys to their handlers.
/// All handlers must be provided via DI — this class does not register any defaults.
/// </summary>
public class SeasonRegistry : ISeasonRegistry
{
    private readonly Dictionary<string, ISeasonHandler> _handlers;
    private readonly List<ISeasonHandler> _all;

    public SeasonRegistry(IEnumerable<ISeasonHandler> handlers)
    {
        _all = handlers.ToList();
        _handlers = new Dictionary<string, ISeasonHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in _all)
            _handlers[handler.SeasonKey] = handler;
    }

    /// <inheritdoc />
    public IReadOnlyList<ISeasonHandler> All => _all;

    /// <inheritdoc />
    public ISeasonHandler? Get(string seasonKey)
        => _handlers.GetValueOrDefault(seasonKey);

    /// <inheritdoc />
    public ISeasonHandler? DetectFromFileName(string fileName)
        => _all.FirstOrDefault(h => h.CanHandle(fileName));
}
