using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Bootstrap.Extensions;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Tools.EditSave.Editing;

public sealed class SaveSession
{
    private static readonly ISeasonRegistry Registry = new ServiceCollection()
        .AddTwdSaveEditorServices()
        .BuildServiceProvider()
        .GetRequiredService<ISeasonRegistry>();

    private SaveSession(SaveSlot slot, ISeasonHandler season)
    {
        Slot = slot;
        Season = season;
    }

    public SaveSlot Slot { get; }

    public ISeasonHandler Season { get; }

    public IChoiceAccessor? Choices => Season.CreateChoiceAccessor(Slot);

    public static SaveSession Create(string slotFileName, int episode)
    {
        var season = Registry.DetectFromFileName(slotFileName)
            ?? throw new ArgumentException($"No season handles the file {slotFileName}.");

        var slot = season.CreateSave(slotFileName, episode);
        slot.DetectedSeasonKey = season.SeasonKey;
        return new SaveSession(slot, season);
    }

    public static SaveSession Load(string directory, string slotFileName)
    {
        var season = Registry.DetectFromFileName(slotFileName)
            ?? throw new ArgumentException($"No season handles the file {slotFileName}.");

        var slot = BundleReader.Read(File.ReadAllBytes(Path.Combine(directory, slotFileName)), slotFileName);
        slot.DetectedSeasonKey = season.SeasonKey;

        if (season is ICompanionFileHandler companion)
        {
            var names = Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>().ToList();
            companion.AttachCompanionFiles(slot, [.. companion.FindCompanionFiles(slotFileName, names)
                .Select(name => new CompanionFile(name, File.ReadAllBytes(Path.Combine(directory, name))))]);
        }

        return new SaveSession(slot, season);
    }

    public IEnumerable<ChoiceDefinition> ChoiceList =>
        Season.IncludedSeasonKeys.Concat(Season.ImportsFromSeasonKeys).Distinct()
            .Select(Registry.Get)
            .OfType<ISeasonHandler>()
            .SelectMany(season => season.Choices);

    public List<string> FileNames =>
        [Slot.FileName, .. Season is ICompanionFileHandler companion ? companion.GetCompanionFileNames(Slot) : []];

    public void RestartFromEpisode(int episode)
    {
        if (Season is not IResumePointHandler resume)
        {
            throw new NotSupportedException($"{Season.Name} saves cannot be restarted from an episode.");
        }

        resume.RestartFromEpisode(Slot, episode);
    }

    public IReadOnlyList<ChapterInfo> Chapters(int episode) =>
        Season is IResumePointHandler resume ? resume.GetChapters(episode) : [];

    public bool RestartFromChapter(int episode, string chapterId)
    {
        if (Season is not IResumePointHandler resume || Chapters(episode).All(chapter => !chapter.Id.Equals(chapterId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        resume.RestartFromChapter(Slot, episode, chapterId);
        return true;
    }

    public InventoryState? Inventory => (Season as IInventoryHandler)?.GetInventory(Slot);

    public bool ChangeInventory(bool addCarried, IReadOnlyList<string> give, IReadOnlyList<string> take)
    {
        if (Season is not IInventoryHandler handler || handler.GetInventory(Slot) is not { Editable: true } state)
        {
            return false;
        }

        var items = state.Held
            .Concat(addCarried ? handler.GetCarriedItems(Slot) : [])
            .Concat(give.Select(Holding))
            .Where(item => !take.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            .GroupBy(item => item.Id)
            .Select(group => group.Last())
            .ToList();

        handler.SetInventory(Slot, items);
        return true;
    }

    private static HeldItem Holding(string text)
    {
        var separator = text.LastIndexOf(':');
        return separator > 0 && int.TryParse(text[(separator + 1)..], out var count) ? new HeldItem(text[..separator], count) : new HeldItem(text);
    }

    public List<CompanionFile> Build()
    {
        var files = new List<CompanionFile> { new(Slot.FileName, BundleWriter.Write(Slot)) };
        if (Season is ICompanionFileHandler companion)
        {
            files.AddRange(companion.BuildCompanionFiles(Slot));
        }

        return files;
    }
}
