namespace TwdSaveEditor.Tools.ValidateEditedSaves.Model;

public sealed record InnerFile(
    uint DefaultSize,
    uint DebugSize,
    uint AsyncSize,
    uint Versions,
    string? DebugHex,
    PropertySetHeader? PropertySet,
    OrderedDictionary<string, object>? Properties);
