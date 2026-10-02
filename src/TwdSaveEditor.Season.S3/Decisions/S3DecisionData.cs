namespace TwdSaveEditor.Season.S3.Decisions;

public sealed record S3DecisionData(IReadOnlyList<S3Decision> Decisions, IReadOnlyList<S3LogicKey> LogicKeys);
