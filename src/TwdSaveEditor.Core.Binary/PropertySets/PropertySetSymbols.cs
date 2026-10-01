using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.PropertySets;

public static class PropertySetSymbols
{
    public static int Count(PropertySet propSet)
    {
        var count = propSet.ParentSymbols.Count;
        foreach (var group in propSet.TypeGroups)
        {
            count += 1 + group.Properties.Count;
            foreach (var property in group.Properties)
            {
                count += property.Value switch
                {
                    SymbolValue => 1,
                    PropertySetValue nested => Count(nested.Value),
                    _ => 0,
                };
            }
        }

        return count;
    }
}
