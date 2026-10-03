using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Tests.DialogLog;

public class NodeExpressionTests
{
    private static readonly Dictionary<string, string> Ids = new()
    {
        ["A"] = "{11111111-1111-1111-1111-111111111111}",
        ["B"] = "22222222-2222-2222-2222-222222222222",
        ["C"] = "{33333333-3333-3333-3333-333333333333}",
    };

    private static NodeExpression Parse(string expression) =>
        NodeExpression.Parse(Ids.Aggregate(expression, (current, id) => current.Replace(id.Key, id.Value)));

    private static ulong Node(string name) => DialogLogEditor.NodeSymbol(Ids[name]);

    [Theory]
    [InlineData("A", "A", true)]
    [InlineData("A | B", "B", true)]
    [InlineData("A & B", "A", false)]
    [InlineData("~A & B", "B", true)]
    [InlineData("~A & B", "A B", false)]
    [InlineData("A & B | C", "C", false)]
    [InlineData("A | B & C", "B", false)]
    [InlineData("(A | B) & C", "B C", true)]
    [InlineData("~(A | B)", "", true)]
    [InlineData("t & ~f", "", true)]
    [InlineData("A &", "A", false)]
    public void Expression_IsEvaluatedTheWayTheGameDoes(string expression, string seen, bool expected)
    {
        var nodes = seen.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Node).ToHashSet();

        Assert.Equal(expected, Parse(expression).Evaluate(nodes));
    }

    [Fact]
    public void Nodes_ListsEachDialogNodeOnceInTheOrderTheyAppear()
    {
        Assert.Equal([Node("B"), Node("A"), Node("C")], Parse("B | (~A & (C | B)) & t").Nodes);
    }

    [Fact]
    public void AnEmptyExpressionIsNeverTrue()
    {
        Assert.True(NodeExpression.Parse(string.Empty).IsEmpty);
        Assert.False(NodeExpression.Parse(string.Empty).Evaluate(new HashSet<ulong>()));
    }
}
