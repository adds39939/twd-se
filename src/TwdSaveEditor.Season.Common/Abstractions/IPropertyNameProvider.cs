namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IPropertyNameProvider
{
    IEnumerable<string> PropertyNames { get; }
}
