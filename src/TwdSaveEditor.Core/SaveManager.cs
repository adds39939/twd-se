using TwdSaveEditor.Core.Binary;
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
    /// </summary>
    public SaveSlot LoadFile(string filePath)
    {
        return BundleReader.Read(filePath);
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
