using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Tools.VerifyRoundTrip.Verification;

public static class RoundTripVerifier
{
    public static RoundTripResult Verify(string path, bool parseAllProperties)
    {
        var problems = new List<string>();
        var original = File.ReadAllBytes(path);

        try
        {
            var slot = BundleReader.Read(original, path);
            if (parseAllProperties)
            {
                foreach (var file in slot.Files.Where(file => file.TypeSymbol == TelltaleTypes.PropertySet))
                    BundleReader.TryParseProperties(file);
            }

            var rewritten = BundleWriter.Write(slot);
            var before = MetaStreamCodec.Read(original);
            var after = MetaStreamCodec.Read(rewritten);

            Compare("default", before.Default, after.Default, problems);
            Compare("debug", before.Debug, after.Debug, problems);
            Compare("async", before.Async, after.Async, problems);

            if (before.Header.DefaultSectionSize >> 31 != after.Header.DefaultSectionSize >> 31
                || before.Header.DebugSectionSize >> 31 != after.Header.DebugSectionSize >> 31
                || before.Header.AsyncSectionSize >> 31 != after.Header.AsyncSectionSize >> 31)
                problems.Add("compression flags differ");

            return new RoundTripResult(path, slot.Files.Count, slot.Files.Count(file => file.Properties != null), problems);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            problems.Add($"{e.GetType().Name}: {e.Message}");
            return new RoundTripResult(path, 0, 0, problems);
        }
    }

    private static void Compare(string section, byte[] before, byte[] after, List<string> problems)
    {
        var common = Math.Min(before.Length, after.Length);
        var difference = before.AsSpan(0, common).CommonPrefixLength(after.AsSpan(0, common));
        if (difference != common)
        {
            problems.Add($"{section} differs at offset {difference} (sizes {before.Length} and {after.Length})");
            return;
        }

        var tail = before.Length > after.Length ? before.AsSpan(common) : after.AsSpan(common);
        if (tail.ContainsAnyExcept((byte)0))
            problems.Add($"{section} sizes differ ({before.Length} and {after.Length}) and the remainder is not padding");
    }
}
