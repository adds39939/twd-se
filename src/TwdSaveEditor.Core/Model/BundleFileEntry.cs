using System.Text;
using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Model;

public sealed class BundleFileEntry
{
    public const int NameFieldSize = 16;

    public required byte[] NameField { get; init; }

    public required ulong NameSymbol { get; init; }

    public required ulong TypeSymbol { get; init; }

    public required byte[] Data { get; set; }

    public PropertySet? Properties { get; set; }

    public uint? OriginalOffset { get; init; }

    public string Name
    {
        get
        {
            var end = Array.IndexOf(NameField, (byte)0);
            return Encoding.Latin1.GetString(NameField, 0, end < 0 ? NameField.Length : end);
        }
    }

    public bool IsNamed(string fileName) => NameSymbol == TelltaleHash.ComputeCrc64(fileName);

    public static BundleFileEntry Create(string fileName, ulong typeSymbol, byte[] data)
    {
        var nameField = new byte[NameFieldSize];
        var nameBytes = Encoding.Latin1.GetBytes(fileName);
        Array.Copy(nameBytes, nameField, Math.Min(nameBytes.Length, NameFieldSize - 1));

        return new BundleFileEntry
        {
            NameField = nameField,
            NameSymbol = TelltaleHash.ComputeCrc64(fileName),
            TypeSymbol = typeSymbol,
            Data = data,
        };
    }
}
