using TwdSaveEditor.Tools.Common.Hashing;

namespace TwdSaveEditor.Tools.DecodeEstore.Analysis;

public static class EventTypeNames
{
    public static readonly Dictionary<ulong, string> ByHash =
        new[] { "Executing Dialog Node", "Begin Episode", "End Episode", "Save Serial", "Dialog Choice" }
            .ToDictionary(TelltaleCrc64.Compute, name => name);

    public static bool IsKnown(ulong hash) => ByHash.ContainsKey(hash);

    public static string Describe(ulong hash, string fallback) => ByHash.GetValueOrDefault(hash, fallback);
}
