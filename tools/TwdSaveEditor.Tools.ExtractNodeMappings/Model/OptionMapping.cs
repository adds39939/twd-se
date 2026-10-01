namespace TwdSaveEditor.Tools.ExtractNodeMappings.Model;

public sealed record OptionMapping(
    string Text,
    string TextClean,
    string Expression,
    string SecondaryExpression,
    string Guid,
    string Percentage);
