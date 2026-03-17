using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core;

/// <summary>
/// Discovers, loads, backs up, and saves TWD:TTDS .bundle save files.
/// </summary>
public sealed class SaveManager
{
    public static readonly string DefaultSavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Telltale Games", "TWDTTDS");

    public string SaveDirectory { get; private set; }

    public SaveManager(string? savePath = null)
    {
        SaveDirectory = savePath ?? DefaultSavePath;
    }

    public void SetSaveDirectory(string path)
    {
        SaveDirectory = path;
    }

    public bool SaveDirectoryExists => Directory.Exists(SaveDirectory);

    /// <summary>
    /// Discover all .bundle save files in the save directory.
    /// </summary>
    public IReadOnlyList<string> DiscoverFiles()
    {
        if (!SaveDirectoryExists)
            return [];

        var files = new List<string>();
        files.AddRange(Directory.GetFiles(SaveDirectory, "*.bundle"));
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    /// <summary>
    /// Load a .bundle save file, parsing its outer MetaStream, file table, and inner PropertySets.
    /// Also discovers associated estore/epage files for S3/Michonne saves.
    /// </summary>
    public SaveSlot LoadFile(string filePath)
    {
        var slot = BundleReader.Read(filePath);
        DiscoverEventLogFiles(slot);
        return slot;
    }

    /// <summary>
    /// Find estore/epage files associated with a slot bundle.
    /// Pattern: wd3_saveslot1.bundle → _wd3_saveslot1_id.estore + _wd3_saveslot1_id_Page*.epage
    /// </summary>
    private static void DiscoverEventLogFiles(SaveSlot slot)
    {
        var dir = Path.GetDirectoryName(slot.FilePath);
        if (dir == null || !Directory.Exists(dir)) return;

        // Derive the estore name from the bundle name
        // wd3_saveslot1.bundle → _wd3_saveslot1_id.estore
        var bundleName = Path.GetFileNameWithoutExtension(slot.FilePath);
        var estoreName = $"_{bundleName}_id.estore";
        var estorePath = Path.Combine(dir, estoreName);

        if (!File.Exists(estorePath)) return;

        slot.EStorePath = estorePath;

        // Find epage files
        var pagePattern = $"_{bundleName}_id_Page*.epage";
        slot.EPagePaths = Directory.GetFiles(dir, pagePattern)
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Save a modified bundle back to disk. Creates a .bak backup first.
    /// </summary>
    public void SaveFile(SaveSlot slot)
    {
        CreateBackup(slot.FilePath);

        var fileBytes = BundleWriter.Write(slot);
        File.WriteAllBytes(slot.FilePath, fileBytes);
    }

    /// <summary>
    /// Create a new blank save and write it to the save directory.
    /// Returns the created SaveSlot.
    /// </summary>
    public SaveSlot CreateNewSave(string fileName, string seasonKey = "s1", int episode = 1)
    {
        var filePath = Path.Combine(SaveDirectory, fileName);
        var slot = SaveSlotFactory.CreateForSeason(seasonKey, episode, fileName);

        // Update the slot with the real file path
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

        // S3/Michonne: write estore/epage files alongside the bundle
        if (seasonKey is "s3" or "michonne")
        {
            var bundleBaseName = Path.GetFileNameWithoutExtension(fileName);
            var slotBaseName = $"_{bundleBaseName}";
            var events = realSlot.PendingEventLogEntries ?? [];

            var (estoreBytes, epageBytes, epageFilename) =
                EStoreCreator.Create(slotBaseName, events);

            var estorePath = Path.Combine(SaveDirectory, $"{slotBaseName}_id.estore");
            var epagePath = Path.Combine(SaveDirectory, epageFilename);

            File.WriteAllBytes(estorePath, estoreBytes);
            File.WriteAllBytes(epagePath, epageBytes);

            realSlot.EStorePath = estorePath;
            realSlot.EPagePaths = [epagePath];
        }

        return realSlot;
    }

    /// <summary>
    /// Creates a .bak backup of the file if one doesn't already exist.
    /// If a .bak exists, creates numbered backups (.bak1, .bak2, etc.)
    /// </summary>
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

        // Find next available numbered backup
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
