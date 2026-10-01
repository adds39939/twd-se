namespace TwdSaveEditor.Tools.ExtractNodeMappings.Model;

public sealed record ChoiceMapping(
    string Question,
    string QuestionClean,
    string QuestionExpression,
    string QuestionSecondary,
    string EpisodeCode,
    string TargetEpisode,
    List<OptionMapping> Options);
