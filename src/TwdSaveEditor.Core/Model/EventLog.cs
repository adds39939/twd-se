using TwdSaveEditor.Core.Hashing;

namespace TwdSaveEditor.Core.Model;

public sealed class EventLog(string storageName, EventLogStorage storage)
{
    public string StorageName { get; } = storageName;

    public EventLogStorage Storage { get; } = storage;

    public List<EventLogPageFile> PageFiles { get; } = [];

    public bool StorageModified { get; set; }

    public IEnumerable<EventLogPage> Pages
    {
        get
        {
            foreach (var entry in Storage.Pages)
            {
                if (FindPageFile(entry.PageSymbol) is { } file)
                    yield return file.Page;
            }

            if (Storage.CurrentPage != null)
                yield return Storage.CurrentPage;
        }
    }

    public IEnumerable<EventLogEvent> Events => Pages.SelectMany(page => page.Events);

    public EventLogPageFile? FindPageFile(ulong symbol) =>
        PageFiles.FirstOrDefault(file => TelltaleHash.ComputeCrc64(file.Name) == symbol);

    public void MarkModified(EventLogPage page)
    {
        if (ReferenceEquals(page, Storage.CurrentPage))
            StorageModified = true;
        else if (PageFiles.FirstOrDefault(file => ReferenceEquals(file.Page, page)) is { } file)
            file.Modified = true;
    }
}
