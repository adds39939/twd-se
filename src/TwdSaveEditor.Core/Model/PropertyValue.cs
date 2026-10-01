namespace TwdSaveEditor.Core.Model;

public abstract class PropertyValue
{
    public abstract string TypeName { get; }
    public abstract object BoxedValue { get; }
}
