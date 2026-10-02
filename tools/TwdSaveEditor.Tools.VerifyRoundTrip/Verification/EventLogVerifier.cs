using TwdSaveEditor.Core.Binary.EventLog;

namespace TwdSaveEditor.Tools.VerifyRoundTrip.Verification;

public static class EventLogVerifier
{
    public const string StorageExtension = ".estore";
    public const string PageExtension = ".epage";

    public static string? Verify(string path)
    {
        var original = File.ReadAllBytes(path);
        try
        {
            var rewritten = path.EndsWith(StorageExtension, StringComparison.OrdinalIgnoreCase)
                ? EventLogCodec.WriteStorage(EventLogCodec.ReadStorage(original))
                : EventLogCodec.WritePage(EventLogCodec.ReadPage(original));

            return rewritten.AsSpan().SequenceEqual(original) ? null : $"rewritten file differs ({original.Length} -> {rewritten.Length} bytes)";
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return e.Message;
        }
    }
}
