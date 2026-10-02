using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.S2.Decisions;

namespace TwdSaveEditor.Season.S2.Saves;

public sealed class S2EventLogEditor(SaveSlot slot)
{
    private EventLog Log => slot.EventLog ?? throw new InvalidOperationException("The save has no event log.");

    public static ulong NodeSymbol(string node) => TelltaleHash.ComputeCrc64("{" + node + "}");

    public string? GetValue(S2Decision decision) =>
        (FindSeen(decision) ?? decision.Options.FirstOrDefault(option => option.Nodes.Count == 0))?.Value;

    public S2DecisionOption? FindSeen(S2Decision decision)
    {
        if (slot.EventLog == null)
            return null;

        var present = Log.Events.Select(entry => entry.DialogNode).OfType<ulong>().ToHashSet();
        return decision.Options.FirstOrDefault(option => option.Nodes.Any(node => present.Contains(NodeSymbol(node))));
    }

    public void Clear(S2Decision decision)
    {
        var nodes = decision.Options.SelectMany(option => option.Nodes).Select(NodeSymbol).ToHashSet();
        foreach (var page in Log.Pages.ToList())
        {
            if (page.Events.RemoveAll(entry => entry.DialogNode is { } node && nodes.Contains(node)) > 0)
                Log.MarkModified(page);
        }
    }

    public void TruncateAfterSerial(long serial)
    {
        var storage = Log.Storage;
        var pages = Log.Pages.ToList();
        var anchorPage = pages.FindLastIndex(page => page.Events.Any(entry => entry.SaveSerial == serial));
        var keep = anchorPage < 0 ? 0 : pages[anchorPage].Events.FindLastIndex(entry => entry.SaveSerial == serial) + 1;

        var unflushed = storage.CurrentPage;
        EventLogPage? remainder = null;
        for (var index = Math.Max(anchorPage, 0); index < pages.Count; index++)
        {
            var page = pages[index];
            var kept = index == anchorPage ? keep : 0;
            if (kept == page.Events.Count)
                continue;

            page.Events.RemoveRange(kept, page.Events.Count - kept);
            if (ReferenceEquals(page, unflushed))
                continue;

            var file = Log.PageFiles.First(candidate => ReferenceEquals(candidate.Page, page));
            Log.PageFiles.Remove(file);
            storage.Pages.RemoveAll(entry => entry.PageSymbol == TelltaleHash.ComputeCrc64(file.Name));
            if (!slot.ObsoleteFileNames.Contains(file.Name))
                slot.ObsoleteFileNames.Add(file.Name);

            if (page.Events.Count > 0)
            {
                page.FlushedName = string.Empty;
                remainder = page;
            }
        }

        storage.CurrentPage = remainder ?? (unflushed is { Events.Count: > 0 } ? unflushed : null);

        storage.LastEventId = Log.Events.Select(entry => entry.Id).DefaultIfEmpty().Max();
        Log.StorageModified = true;
    }

    public void SetValue(S2Decision decision, S2DecisionOption target)
    {
        var wanted = target.Nodes.Select(NodeSymbol).ToHashSet();
        var others = decision.Options.Where(option => !ReferenceEquals(option, target))
            .SelectMany(option => option.Nodes)
            .Select(NodeSymbol)
            .Where(node => !wanted.Contains(node))
            .ToHashSet();

        var placed = Log.Events.Any(entry => entry.DialogNode is { } node && wanted.Contains(node));
        foreach (var page in Log.Pages.ToList())
        {
            for (var index = page.Events.Count - 1; index >= 0; index--)
            {
                if (page.Events[index].DialogNode is not { } node || !others.Contains(node))
                    continue;

                if (!placed && wanted.Count > 0)
                {
                    page.Events[index].SetDialogNode(NodeSymbol(target.Nodes[0]));
                    placed = true;
                }
                else
                {
                    page.Events.RemoveAt(index);
                }

                Log.MarkModified(page);
            }
        }

        if (!placed && wanted.Count > 0)
            Insert(NodeSymbol(target.Nodes[0]), decision.Episode);

        foreach (var required in decision.Requires.Select(NodeSymbol))
        {
            if (!Log.Events.Any(entry => entry.DialogNode == required))
                Insert(required, decision.Episode);
        }
    }

    private void Insert(ulong node, int episode)
    {
        var used = Log.Events.Select(entry => entry.Id).ToHashSet();
        var anchor = FindAnchor(episode);
        if (anchor == null)
        {
            Append(node, used);
            return;
        }

        var (page, index) = anchor.Value;
        var id = page.Events[index].Id;
        var floor = index > 0
            ? page.Events.Take(index).Min(entry => entry.Id)
            : Log.Pages.TakeWhile(earlier => !ReferenceEquals(earlier, page)).Select(earlier => earlier.MaxEventId).DefaultIfEmpty().Max();
        while (id > floor && used.Contains(id))
            id--;

        if ((used.Contains(id) || id == 0) && !MakeRoom(page, index, out id))
        {
            Append(node, used);
            return;
        }

        page.Events.Insert(index, EventLogEvent.ForDialogNode(id, node));
        Log.MarkModified(page);
    }

    private bool MakeRoom(EventLogPage page, int index, out uint id)
    {
        var storage = Log.Storage;
        id = page.Events[index].Id;
        if (!ReferenceEquals(page, storage.CurrentPage) || Log.PageFiles.Any(file => file.Page.Events.Any(entry => entry.Id > page.Events[index].Id)))
            return false;

        var first = id;
        foreach (var later in page.Events.Where(entry => entry.Id >= first))
            later.Id++;

        storage.LastEventId = Math.Max(storage.LastEventId, page.Events.Max(entry => entry.Id));
        return true;
    }

    private (EventLogPage Page, int Index)? FindAnchor(int episode)
    {
        foreach (var serial in AnchorSerials(episode))
        {
            foreach (var page in Log.Pages)
            {
                var index = page.Events.FindLastIndex(entry => entry.SaveSerial == serial);
                if (index >= 0)
                    return (page, index);
            }
        }

        return null;
    }

    private IEnumerable<long> AnchorSerials(int episode)
    {
        var saves = slot.Checkpoints
            .Select(save => (Episode: S2SlotFiles.EpisodeNumber(save.Metadata?.GetString(SaveMetadataKeys.Episode)), Serial: (long)(save.Metadata?.GetInt(SaveMetadataKeys.Serial) ?? 0)))
            .Where(save => save.Episode != null && save.Serial > 0)
            .ToList();

        var own = saves.Where(save => save.Episode <= episode).OrderByDescending(save => save.Serial).Select(save => save.Serial);
        var later = saves.Where(save => save.Episode > episode).OrderBy(save => save.Serial).Select(save => save.Serial);
        var latest = (long)(slot.Metadata?.GetInt(SlotMetadataKeys.LatestSerial) ?? 0);
        return own.Concat(later).Append(latest).Where(serial => serial > 0);
    }

    public void AppendSaveSerial(int serial) =>
        Append(id => EventLogEvent.ForSaveSerial(id, serial), Log.Events.Select(entry => entry.Id).ToHashSet());

    private void Append(ulong node, HashSet<uint> used) => Append(id => EventLogEvent.ForDialogNode(id, node), used);

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
