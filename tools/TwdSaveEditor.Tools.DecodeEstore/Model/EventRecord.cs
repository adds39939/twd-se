namespace TwdSaveEditor.Tools.DecodeEstore.Model;

public sealed record EventRecord(int Index, byte[] Raw, string? EventType);
