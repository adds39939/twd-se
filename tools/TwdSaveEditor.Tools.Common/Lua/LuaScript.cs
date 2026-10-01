using TwdSaveEditor.Tools.Common.Cryptography;

namespace TwdSaveEditor.Tools.Common.Lua;

public static class LuaScript
{
    private const int HeaderSize = 4;

    private static readonly byte[] CompiledHeader = "\u001bLua"u8.ToArray();
    private static readonly byte[] EncryptedCompiledHeader = "\u001bLEn"u8.ToArray();
    private static readonly byte[] EncryptedSourceHeader = "\u001bLEo"u8.ToArray();

    public static bool IsCompiled(ReadOnlySpan<byte> data) => data.StartsWith(CompiledHeader);

    public static byte[] Decrypt(ReadOnlySpan<byte> data, BlowfishV7 cipher)
    {
        if (data.StartsWith(EncryptedCompiledHeader))
        {
            var result = data.ToArray();
            cipher.Decrypt(result.AsSpan(HeaderSize));
            CompiledHeader.CopyTo(result, 0);
            return result;
        }

        if (data.StartsWith(EncryptedSourceHeader))
        {
            var result = data[HeaderSize..].ToArray();
            cipher.Decrypt(result);
            return result;
        }

        return data.ToArray();
    }
}
