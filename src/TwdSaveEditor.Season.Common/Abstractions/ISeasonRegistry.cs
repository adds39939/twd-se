namespace TwdSaveEditor.Season.Common.Abstractions;

public interface ISeasonRegistry
{
    ISeasonHandler? Get(string seasonKey);

    ISeasonHandler? DetectFromFileName(string fileName);

    IReadOnlyList<ISeasonHandler> All { get; }
}
