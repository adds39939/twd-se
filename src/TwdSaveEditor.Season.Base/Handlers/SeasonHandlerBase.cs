using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Resources;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Handlers;

public abstract class SeasonHandlerBase : ISeasonHandler
{
    private readonly Lazy<IReadOnlyList<ChoiceDefinition>> _choices;
    private readonly Lazy<IReadOnlyDictionary<string, string[]>> _scenes;
    private IReadOnlyList<string>? _includedSeasonKeys;

    protected SeasonHandlerBase()
    {
        var assembly = GetType().Assembly;
        _choices = new(() => EmbeddedSeasonData.LoadChoices(assembly, SeasonKey));
        _scenes = new(() => EmbeddedSeasonData.LoadScenes(assembly));
    }

    public abstract string SeasonKey { get; }
    public abstract string Name { get; }
    public abstract string ShortName { get; }
    public abstract string FilePrefix { get; }
    public abstract IReadOnlyList<EpisodeInfo> Episodes { get; }

    public IReadOnlyList<ChoiceDefinition> Choices => _choices.Value;

    public virtual IReadOnlyList<string> IncludedSeasonKeys => _includedSeasonKeys ??= [SeasonKey];

    public virtual IReadOnlyList<string> ImportsFromSeasonKeys => [];

    public abstract string GetEpisodeId(int episode);

    public virtual (string SeasonKey, int Episode) DecisionGroupOf(int episode) => (SeasonKey, episode);

    public IReadOnlyList<string> GetScenes(string episodeId)
        => _scenes.Value.TryGetValue(episodeId, out var scenes) ? scenes : [];

    public virtual bool CanHandle(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        if (name.StartsWith('_')) name = name[1..];
        return name.StartsWith(FilePrefix, StringComparison.Ordinal);
    }

    public abstract SaveSlot CreateBlankSave(string fileName, string episodeId);
    public abstract IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    public abstract void PopulateChoices(SaveSlot slot, int episode);

    protected List<ChoiceDefinition> GetChoicesUpTo(int episode)
        => Choices.Where(c => c.Episode <= episode).ToList();
}
