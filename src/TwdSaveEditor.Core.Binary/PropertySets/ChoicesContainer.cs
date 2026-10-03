namespace TwdSaveEditor.Core.Binary.PropertySets;

public static class ChoicesContainer
{
    public static List<(string str, bool boolVal)> Parse(byte[] data)
    {
        var result = new List<(string, bool)>();
        var pos = 0;

        if (data.Length < 4)
        {
            return result;
        }

        var count = BitConverter.ToUInt32(data, pos);
        pos += 4;

        for (uint i = 0; i < count && pos < data.Length; i++)
        {
            var strLen = BitConverter.ToInt32(data, pos);
            pos += 4;
            var str = System.Text.Encoding.Latin1.GetString(data, pos, strLen);
            pos += strLen;
            var boolVal = pos < data.Length && data[pos] == 0x31;
            pos++;
            result.Add((str, boolVal));
        }

        return result;
    }

    public static byte[] Serialize(List<(string str, bool boolVal)> entries)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((uint)entries.Count);
        foreach (var (str, boolVal) in entries)
        {
            var bytes = System.Text.Encoding.Latin1.GetBytes(str);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Write((byte)(boolVal ? 0x31 : 0x30));
        }

        return ms.ToArray();
    }
}
