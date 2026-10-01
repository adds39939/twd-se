namespace TwdSaveEditor.Core.Model;

public sealed class TypeGroup(Symbol typeSymbol)
{
    public Symbol TypeSymbol { get; } = typeSymbol;
    public List<Property> Properties { get; set; } = [];
}
