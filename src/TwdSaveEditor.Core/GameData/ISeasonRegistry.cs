namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Abstraction over the season handler registry, enabling DI.
/// </summary>
public interface ISeasonRegistry
{
    /// <summary>Get a handler by season key, or null if not found.</summary>
    ISeasonHandler? Get(string seasonKey);

    /// <summary>Detect the handler from a file name, or null if no match.</summary>
    ISeasonHandler? DetectFromFileName(string fileName);

    /// <summary>All registered handlers.</summary>
    IReadOnlyList<ISeasonHandler> All { get; }
}
