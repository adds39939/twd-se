namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record ClassLayout(string Type, uint Version, List<ClassMember> Members);
