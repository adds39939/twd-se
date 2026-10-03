using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using GameLog = TwdSaveEditor.Core.Model.EventLog;

namespace TwdSaveEditor.Season.Base.DialogLog;

public sealed class DialogLogEditor(SaveSlot slot)
{
    public GameLog Log => slot.EventLog ?? throw new InvalidOperationException("The save has no event log.");

    public bool Exists => slot.EventLog != null;

    public static ulong NodeSymbol(string node) => TelltaleHash.ComputeCrc64("{" + node.Trim('{', '}') + "}");

    public HashSet<ulong> Nodes() =>
        slot.EventLog == null ? [] : [.. Log.Events.Select(entry => entry.DialogNode).OfType<ulong>()];

    public void RemoveNodes(IReadOnlyCollection<ulong> nodes)
    {
        foreach (var page in Log.Pages.ToList())
        {
            if (page.Events.RemoveAll(entry => entry.DialogNode is { } node && nodes.Contains(node)) > 0)
            {
                Log.MarkModified(page);
            }
        }
    }

    public (EventLogPage Page, int Index)? FindFirst(Func<EventLogEvent, bool> match)
    {
        foreach (var page in Log.Pages)
        {
            var index = page.Events.FindIndex(entry => match(entry));
            if (index >= 0)
            {
                return (page, index);
            }
        }

        return null;
    }

    public (EventLogPage Page, int Index)? FindLast(Func<EventLogEvent, bool> match)
    {
        (EventLogPage, int)? found = null;
        foreach (var page in Log.Pages)
        {
            var index = page.Events.FindLastIndex(entry => match(entry));
            if (index >= 0)
            {
                found = (page, index);
            }
        }

        return found;
    }

    public void TruncateFrom((EventLogPage Page, int Index)? position)
    {
        var storage = Log.Storage;
        var pages = Log.Pages.ToList();
        var anchorPage = position == null ? -1 : pages.FindIndex(page => ReferenceEquals(page, position.Value.Page));
        var keep = position?.Index ?? 0;

        var unflushed = storage.CurrentPage;
        EventLogPage? remainder = null;
        for (var index = Math.Max(anchorPage, 0); index < pages.Count; index++)
        {
            var page = pages[index];
            var kept = index == anchorPage ? keep : 0;
            if (kept == page.Events.Count)
            {
                continue;
            }

            page.Events.RemoveRange(kept, page.Events.Count - kept);
            if (ReferenceEquals(page, unflushed))
            {
                continue;
            }

            var file = Log.PageFiles.First(candidate => ReferenceEquals(candidate.Page, page));
            Log.PageFiles.Remove(file);
            storage.Pages.RemoveAll(entry => entry.PageSymbol == TelltaleHash.ComputeCrc64(file.Name));
            if (!slot.ObsoleteFileNames.Contains(file.Name))
            {
                slot.ObsoleteFileNames.Add(file.Name);
            }

            if (page.Events.Count > 0)
            {
                page.FlushedName = string.Empty;
                page.Compressed = false;
                page.DebugCompressed = false;
                remainder = page;
            }
        }

        storage.CurrentPage = remainder ?? (unflushed is { Events.Count: > 0 } ? unflushed : null);
        storage.LastEventId = Log.Events.Select(entry => entry.Id).DefaultIfEmpty().Max();
        Log.StorageModified = true;
    }

    public void InsertBefore((EventLogPage Page, int Index)? anchor, Func<uint, EventLogEvent> create)
    {
        var used = Log.Events.Select(entry => entry.Id).ToHashSet();
        if (anchor == null)
        {
            Append(create, used);
            return;
        }

        var (page, index) = anchor.Value;
        var id = page.Events[index].Id;
        var floor = index > 0
            ? page.Events.Take(index).Min(entry => entry.Id)
            : Log.Pages.TakeWhile(earlier => !ReferenceEquals(earlier, page)).Select(earlier => earlier.MaxEventId).DefaultIfEmpty().Max();
        while (id > floor && used.Contains(id))
        {
            id--;
        }

        if ((used.Contains(id) || id == 0) && !MakeRoom(page, index, out id))
        {
            Append(create, used);
            return;
        }

        page.Events.Insert(index, create(id));
        Log.MarkModified(page);
    }

    public void Append(Func<uint, EventLogEvent> create) => Append(create, Log.Events.Select(entry => entry.Id).ToHashSet());

    public void InsertManyBefore((EventLogPage Page, int Index) anchor, IReadOnlyList<Func<uint, EventLogEvent>> creators)
    {
        var (page, index) = anchor;
        var used = Log.Events.Select(entry => entry.Id).ToHashSet();
        var limit = page.Events[index].Id;
        var floor = index > 0
            ? page.Events.Take(index).Max(entry => entry.Id)
            : Log.Pages.TakeWhile(earlier => !ReferenceEquals(earlier, page)).Select(earlier => earlier.MaxEventId).DefaultIfEmpty().Max();

        var ids = new List<uint>();
        for (var id = limit - 1; id > floor && ids.Count < creators.Count; id--)
        {
            if (!used.Contains(id))
            {
                ids.Add(id);
            }
        }

        ids.Reverse();
        var missing = creators.Count - ids.Count;
        if (missing > 0)
        {
            if (!ReferenceEquals(page, Log.Storage.CurrentPage) || Log.PageFiles.Any(file => file.Page.Events.Any(entry => entry.Id > limit)))
            {
                throw new InvalidOperationException("The event log has no room for the events at that position.");
            }

            foreach (var later in page.Events.Where(entry => entry.Id >= limit))
            {
                later.Id += (uint)missing;
            }

            ids.AddRange(Enumerable.Range(0, missing).Select(offset => limit + (uint)offset));
            Log.Storage.LastEventId = Math.Max(Log.Storage.LastEventId, page.Events.Max(entry => entry.Id));
        }

        page.Events.InsertRange(index, creators.Select((create, position) => create(ids[position])));
        Log.MarkModified(page);
    }

    private bool MakeRoom(EventLogPage page, int index, out uint id)
    {
        var storage = Log.Storage;
        id = page.Events[index].Id;
        if (!ReferenceEquals(page, storage.CurrentPage) || Log.PageFiles.Any(file => file.Page.Events.Any(entry => entry.Id > page.Events[index].Id)))
        {
            return false;
        }

        var first = id;
        foreach (var later in page.Events.Where(entry => entry.Id >= first))
        {
            later.Id++;
        }

        storage.LastEventId = Math.Max(storage.LastEventId, page.Events.Max(entry => entry.Id));
        return true;
    }

    private void Append(Func<uint, EventLogEvent> create, HashSet<uint> used)
    {
        var storage = Log.Storage;
        storage.CurrentPage ??= new EventLogPage { Version = storage.Version, SessionId = storage.SessionId };

        var id = Math.Max(storage.LastEventId, used.Count == 0 ? 0 : used.Max()) + 1;
        storage.CurrentPage.Events.Add(create(id));
        storage.LastEventId = id;
        Log.StorageModified = true;
    }
}
