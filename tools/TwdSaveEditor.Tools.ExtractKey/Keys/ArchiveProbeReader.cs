using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Binary;

namespace TwdSaveEditor.Tools.ExtractKey.Keys;

public static class ArchiveProbeReader
{
    private const int ProbeSize = 1024;
    private const int HeaderSize = 28;

    public static ArchiveProbe? Read(string archivesDirectory)
    {
        var archives = Directory.GetFiles(archivesDirectory, "*" + EcttArchive.Extension).Order(StringComparer.Ordinal);
        foreach (var archive in archives)
        {
            using var file = File.OpenRead(archive);
            var header = new byte[HeaderSize];
            if (file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length)
            {
                continue;
            }

            if (!header.AsSpan(0, 4).SequenceEqual(EcttArchive.Magic))
            {
                continue;
            }

            var chunkSize = Bytes.U32(header, 4);
            var chunkCount = Bytes.U32(header, 8);
            if (chunkCount == 0)
            {
                continue;
            }

            var rawSize = Bytes.U64(header, 20) - Bytes.U64(header, 12);
            var data = new byte[(int)Math.Min(rawSize, ProbeSize) / 8 * 8];
            file.Position = 12 + 8L * (chunkCount + 1);
            file.ReadExactly(data);
            return new ArchiveProbe(Path.GetFileName(archive), data, rawSize < chunkSize);
        }

        return null;
    }
}
