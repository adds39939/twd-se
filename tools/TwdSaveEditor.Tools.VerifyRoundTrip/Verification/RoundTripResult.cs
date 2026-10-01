namespace TwdSaveEditor.Tools.VerifyRoundTrip.Verification;

public sealed record RoundTripResult(string Path, int FileCount, int ParsedPropertyFiles, List<string> Problems);
