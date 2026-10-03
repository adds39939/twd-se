using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Parsing;

public static class ChoicesContainerScanner
{
    public const ulong TypeHash = 0x8AD17AD4CB809956;

    private const uint MaxProperties = 20;
    private const uint MaxEntries = 500;
    private const uint MaxTextLength = 2000;

    private static readonly byte[] TypeHashBytes = Bytes.FromU64(TypeHash);

    public static List<string> Scan(ReadOnlySpan<byte> data)
    {
        var entries = new List<string>();
        var position = 0;
        while (position < data.Length - 8)
        {
            var index = Bytes.IndexOf(data, TypeHashBytes, position);
            if (index < 0)
            {
                break;
            }

            position = index + 1;
            long read = index + 8;
            if (read + 4 > data.Length)
            {
                continue;
            }

            var propertyCount = Bytes.U32(data, read);
            if (propertyCount > MaxProperties)
            {
                continue;
            }

            read += 4;
            for (uint property = 0; property < propertyCount; property++)
            {
                if (read + 12 > data.Length)
                {
                    break;
                }

                var entryCount = Bytes.U32(data, read + 8);
                read += 12;
                if (entryCount > MaxEntries)
                {
                    break;
                }

                for (uint entry = 0; entry < entryCount; entry++)
                {
                    if (read + 4 > data.Length)
                    {
                        break;
                    }

                    var length = Bytes.U32(data, read);
                    read += 4;
                    if (length > MaxTextLength || read + length + 1 > data.Length)
                    {
                        break;
                    }

                    entries.Add(TextFormat.DecodeAscii(data.Slice((int)read, (int)length)));
                    read += length + 1;
                }
            }
        }

        return entries;
    }
}
