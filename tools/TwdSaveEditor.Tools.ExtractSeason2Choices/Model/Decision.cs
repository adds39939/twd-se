namespace TwdSaveEditor.Tools.ExtractSeason2Choices.Model;

public sealed class Decision
{
    public required int Episode { get; set; }

    public string? RandomizerId { get; init; }

    public List<DecisionOption> Options { get; } = [];

    public List<string> Requires { get; } = [];

    public StatChoice? Stat { get; set; }

    public LogicKey? Logic { get; set; }

    public IEnumerable<string> AllNodes => Options.SelectMany(option => option.Nodes);
}
