using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Tests.DialogLog;

public class NodeExpressionTests
{
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
    public void Expression_IsEvaluatedTheWayTheGameDoes(string expression, string seen, bool expected)
    {
        var ids = new Dictionary<string, string>
        {
            ["A"] = "{11111111-1111-1111-1111-111111111111}",
            ["B"] = "22222222-2222-2222-2222-222222222222",
            ["C"] = "{33333333-3333-3333-3333-333333333333}",
        };
        var text = ids.Aggregate(expression, (current, id) => current.Replace(id.Key, id.Value));
        var nodes = seen.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => DialogLogEditor.NodeSymbol(ids[name])).ToHashSet();

        Assert.Equal(expected, NodeExpression.Evaluate(text, nodes));
    }
}
