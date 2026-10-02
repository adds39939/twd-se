using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Binary.MetaStream;

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

            return rewritten.AsSpan().SequenceEqual(original) || SameContent(original, rewritten)
                ? null
                : $"rewritten file differs ({original.Length} -> {rewritten.Length} bytes)";
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return e.Message;
        }
    }

    private static bool SameContent(byte[] original, byte[] rewritten)
    {
        var before = MetaStreamCodec.Read(original);
        var after = MetaStreamCodec.Read(rewritten);
        return before.Header.IsDefaultCompressed
            && after.Header.IsDefaultCompressed
            && before.Header.IsDebugCompressed == after.Header.IsDebugCompressed
            && before.Header.VersionEntries.Select(entry => (entry.TypeCrc, entry.VersionCrc)).SequenceEqual(after.Header.VersionEntries.Select(entry => (entry.TypeCrc, entry.VersionCrc)))
            && before.Default.AsSpan().SequenceEqual(after.Default)
            && before.Debug.AsSpan().SequenceEqual(after.Debug)
            && before.Async.AsSpan().SequenceEqual(after.Async);
    }
}
