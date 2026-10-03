using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.UI.Services;

public static class SaveArchive
{
    public const string DeleteListName = "files-to-delete.txt";

    private const string DeleteListHeading = "Delete these files from your save folder:";

    public static string Name(string saveFileName) => Path.GetFileNameWithoutExtension(saveFileName) + ".zip";

    public static byte[] Create(IReadOnlyList<CompanionFile> files, IReadOnlyCollection<string> obsoleteFileNames)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                using var entry = archive.CreateEntry(file.Name, CompressionLevel.Optimal).Open();
                entry.Write(file.Data);
            }

            if (obsoleteFileNames.Count > 0)
            {
                using var writer = new StreamWriter(archive.CreateEntry(DeleteListName).Open(), Encoding.UTF8);
                writer.Write(string.Join('\n', obsoleteFileNames.Prepend(DeleteListHeading)) + '\n');
            }
        }

        return stream.ToArray();
    }
}
