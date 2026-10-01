using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.EventLog;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;

namespace TwdSaveEditor.Season.Base.Services;

public sealed class SaveManager
{
    public static readonly string DefaultSavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Telltale Games", "The Walking Dead Definitive");

    private readonly ISeasonRegistry _registry;

    public string SaveDirectory { get; private set; }

    public SaveManager(ISeasonRegistry registry, string? savePath = null)
    {
        _registry = registry;
        SaveDirectory = savePath ?? DefaultSavePath;
    }

    public void SetSaveDirectory(string path)
    {
        SaveDirectory = path;
    }

    public bool SaveDirectoryExists => Directory.Exists(SaveDirectory);

    public IReadOnlyList<string> DiscoverFiles()
    {
        if (!SaveDirectoryExists)
            return [];

        var files = new List<string>();
        files.AddRange(Directory.GetFiles(SaveDirectory, "*.bundle"));
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    public SaveSlot LoadFile(string filePath)
    {
        var slot = BundleReader.Read(filePath);
        slot.DetectedSeasonKey = _registry.DetectFromFileName(filePath)?.SeasonKey;
        DiscoverEventLogFiles(slot);
        return slot;
    }

    private static void DiscoverEventLogFiles(SaveSlot slot)
    {
        var dir = Path.GetDirectoryName(slot.FilePath);
        if (dir == null || !Directory.Exists(dir)) return;

        var estorePath = Path.Combine(dir, EventLogFiles.GetEStoreName(slot.FilePath));

        if (!File.Exists(estorePath)) return;

        slot.EStorePath = estorePath;

        var pagePattern = $"{EventLogFiles.GetEPagePrefix(slot.FilePath)}*{EventLogFiles.EPageExtension}";
        slot.EPagePaths = Directory.GetFiles(dir, pagePattern)
            .OrderBy(f => f)
            .ToList();
    }

    public void SaveFile(SaveSlot slot)
    {
        CreateBackup(slot.FilePath);

        var fileBytes = BundleWriter.Write(slot);
        File.WriteAllBytes(slot.FilePath, fileBytes);
    }

    public SaveSlot CreateNewSave(string fileName, string seasonKey = "s1", int episode = 1)
    {
        var filePath = Path.Combine(SaveDirectory, fileName);
        var slot = _registry.CreateSave(seasonKey, episode, fileName);

        var realSlot = new SaveSlot
        {
            FilePath = filePath,
            FileName = fileName,
            OuterHeader = slot.OuterHeader,
            FileTable = slot.FileTable,
            Metadata = slot.Metadata,
            Choices = slot.Choices,
            ChoiceStats = slot.ChoiceStats,
            RawMetadataFile = slot.RawMetadataFile,
            RawChoicesFile = slot.RawChoicesFile,
            RawChoiceStatsFile = slot.RawChoiceStatsFile,
            RawInnerFiles = slot.RawInnerFiles,
            PendingEventLogEntries = slot.PendingEventLogEntries,
        };

        Directory.CreateDirectory(SaveDirectory);
        var fileBytes = BundleWriter.Write(realSlot);
        File.WriteAllBytes(filePath, fileBytes);

        if (_registry.Get(seasonKey) is ICompanionFileHandler companion)
        {
            foreach (var file in companion.BuildCompanionFiles(realSlot))
                File.WriteAllBytes(Path.Combine(SaveDirectory, file.Name), file.Data);

            DiscoverEventLogFiles(realSlot);
        }

        return realSlot;
    }

    public static void CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var bakPath = filePath + ".bak";
        if (!File.Exists(bakPath))
        {
            File.Copy(filePath, bakPath);
            return;
        }

        for (int i = 1; i < 100; i++)
        {
            var numberedPath = $"{filePath}.bak{i}";
            if (!File.Exists(numberedPath))
            {
                File.Copy(filePath, numberedPath);
                return;
            }
        }
    }
}
