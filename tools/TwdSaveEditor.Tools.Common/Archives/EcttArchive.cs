using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Compression;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Archives;

public static class EcttArchive
{
    public const string Extension = ".ttarch2";

    public static readonly byte[] Magic = "ECTT"u8.ToArray();

    public static byte[]? Read(string path, BlowfishV7 cipher, TextWriter? log = null)
    {
        log ??= TextWriter.Null;

        using var file = File.OpenRead(path);
        var header = new byte[12];
        file.ReadExactly(header);

        if (!header.AsSpan(0, 4).SequenceEqual(Magic))
        {
            log.WriteLine($"Magic: 0x{Bytes.U32(header, 0):X8} ({TextFormat.QuoteBytes(header.AsSpan(0, 4))})");
            log.WriteLine("ERROR: Not an ECTT archive!");
            return null;
        }

        var chunkSize = Bytes.U32(header, 4);
        var chunkCount = Bytes.U32(header, 8);
        log.WriteLine($"Chunk size: 0x{chunkSize:X} ({chunkSize})");
        log.WriteLine($"Chunk count: {chunkCount}");

        var offsetTable = new byte[(chunkCount + 1) * 8];
        file.ReadExactly(offsetTable);

        var chunkSizes = new long[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            chunkSizes[i] = (long)(Bytes.U64(offsetTable, (i + 1) * 8L) - Bytes.U64(offsetTable, i * 8L));
        }

        log.WriteLine($"Data starts at offset: 0x{file.Position:X}");
        log.WriteLine($"First 5 chunk sizes: [{string.Join(", ", chunkSizes.Take(5))}]");

        using var assembled = new MemoryStream();
        for (var index = 0; index < chunkCount; index++)
        {
            var rawSize = chunkSizes[index];
            var raw = new byte[rawSize];
            var read = file.ReadAtLeast(raw, raw.Length, throwOnEndOfStream: false);
            if (read != rawSize)
            {
                log.WriteLine($"WARNING: Chunk {index}: expected {rawSize} bytes, got {read}");
                break;
            }

            cipher.Decrypt(raw);

            if (rawSize < chunkSize)
            {
                var inflated = Zlib.TryInflateAny(raw, Zlib.ZlibWindow, Zlib.RawWindow);
                if (inflated == null && index < 3)
                {
                    log.WriteLine($"  Chunk {index}: decompression failed, using raw decrypted");
                }

                assembled.Write(inflated ?? raw);
            }
            else
            {
                assembled.Write(raw);
            }

            if (index < 3 || index == chunkCount - 1)
            {
                log.WriteLine($"  Chunk {index}: raw={rawSize}, total_assembled={assembled.Length}");
            }
        }

        log.WriteLine();
        log.WriteLine($"Total reassembled data: {assembled.Length} bytes");
        return assembled.ToArray();
    }
}
