using TwdSaveEditor.Tools.Common.Cryptography;

namespace TwdSaveEditor.Tools.ValidateSaves.Lua;

public static class LuaDecryptor
{
    private const int HeaderSize = 4;
    private const int BlockSize = 8;

    private static readonly byte[] Key =
    [
        0x63, 0x47, 0xF5, 0x60, 0xC4, 0x12, 0x52, 0xCB,
        0xEF, 0x04, 0x5A, 0x39, 0x41, 0xBC, 0x61, 0xFD,
    ];

    public static bool IsEncrypted(ReadOnlySpan<byte> data) => data.StartsWith("LEn"u8);

    public static bool IsCompiled(ReadOnlySpan<byte> data) => data.StartsWith("\x1bLua"u8) || data.StartsWith("LJ"u8);

    public static byte[] Decrypt(byte[] data)
    {
        if (data.Length < HeaderSize || !IsEncrypted(data))
            return data;

        var encrypted = new byte[(data.Length - HeaderSize + BlockSize - 1) / BlockSize * BlockSize];
        data.AsSpan(HeaderSize).CopyTo(encrypted);

        var decrypted = new StandardBlowfish(Key).DecryptEcb(encrypted);
        return decrypted.AsSpan().TrimEnd((byte)0).ToArray();
    }
}
