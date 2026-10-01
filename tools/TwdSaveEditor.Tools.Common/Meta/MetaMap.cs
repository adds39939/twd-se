namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaMap(List<KeyValuePair<MetaNode, MetaNode>> Entries) : MetaNode
{
    public IEnumerable<KeyValuePair<string, MetaNode>> StringEntries =>
        Entries.Where(entry => entry.Key is MetaScalar { Text: not null })
            .Select(entry => KeyValuePair.Create(((MetaScalar)entry.Key).Text!, entry.Value));

    public string? FindText(string key) =>
        StringEntries.Where(entry => entry.Key == key).Select(entry => (entry.Value as MetaScalar)?.Text).FirstOrDefault();
}
