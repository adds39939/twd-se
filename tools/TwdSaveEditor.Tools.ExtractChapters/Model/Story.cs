namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record Story(int Number, string ChoiceKey, string MirrorFlag)
{
    public string CompleteFlag => $"{Number} - Complete";
}
