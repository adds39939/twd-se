using TwdSaveEditor.Tools.Common.Configuration;

namespace TwdSaveEditor.Tools.Common.Cryptography;

public static class KeyFile
{
    public static byte[] Load()
    {
        if (!File.Exists(ToolPaths.KeyFile))
        {
            throw new FileNotFoundException(
                "Encryption key not found. Run the TwdSaveEditor.Tools.ExtractKey tool, or create tools/key.txt with the Blowfish key hex string. " +
                "The key can be extracted from WDC.exe at offset 0xC3D7A0 (55 bytes).");
        }

        return Convert.FromHexString(File.ReadAllText(ToolPaths.KeyFile).Trim());
    }
}
