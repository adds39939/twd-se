namespace TwdSaveEditor.Core.Model;

public sealed class EventLogPageFile(string name, EventLogPage page)
{
    public string Name { get; } = name;

    public EventLogPage Page { get; } = page;

    public bool Modified { get; set; }
}
