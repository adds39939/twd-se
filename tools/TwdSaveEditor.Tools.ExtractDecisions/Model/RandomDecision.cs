namespace TwdSaveEditor.Tools.ExtractDecisions.Model;

public sealed record RandomDecision(string Id, int Episode, List<List<string>> Checks, List<string> Generated, List<string> Always);
