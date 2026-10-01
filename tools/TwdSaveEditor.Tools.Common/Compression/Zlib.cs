using System.IO.Compression;

namespace TwdSaveEditor.Tools.Common.Compression;

public static class Zlib
{
    public const int ZlibWindow = 15;
    public const int RawWindow = -15;

    public static byte[]? TryInflate(ReadOnlySpan<byte> data, int windowBits)
    {
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using Stream inflater = windowBits > 0
                ? new ZLibStream(input, CompressionMode.Decompress)
                : new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            inflater.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            return null;
        }
    }

    public static byte[]? TryInflateAny(ReadOnlySpan<byte> data, params int[] windowBits)
    {
        foreach (var bits in windowBits)
        {
            var result = TryInflate(data, bits);
            if (result != null)
                return result;
        }

        return null;
    }

    public static byte[] InflatePrefix(ReadOnlySpan<byte> data, int windowBits, int maxLength)
    {
        var buffer = new byte[maxLength];
        var total = 0;
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using Stream inflater = windowBits > 0
                ? new ZLibStream(input, CompressionMode.Decompress)
                : new DeflateStream(input, CompressionMode.Decompress);
            while (total < maxLength)
            {
                var read = inflater.Read(buffer, total, maxLength - total);
                if (read == 0)
                    break;

                total += read;
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
        }

        return buffer[..total];
    }
}
