namespace TwdSaveEditor.Season.S3.Decisions;

public sealed record S3LogicKey(string Key, int ReadFrom, bool Text, IReadOnlyList<S3LogicValue> Values);
