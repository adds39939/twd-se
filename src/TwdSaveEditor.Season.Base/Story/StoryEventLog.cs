using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryEventLog(SaveSlot slot, StorySeason season)
{
    public static readonly ulong PreviousGameBegin = TelltaleHash.ComputeCrc64("Previous Game Data Begin");
    public static readonly ulong PreviousGameEnd = TelltaleHash.ComputeCrc64("Previous Game Data End");
    public static readonly ulong LocalSave = TelltaleHash.ComputeCrc64("Local Save");

    private readonly DialogLogEditor _editor = new(slot);

    public HashSet<ulong> Nodes() => _editor.Nodes();

    public bool HasPreviousGameData => _editor.Exists && _editor.FindFirst(entry => entry.Has(PreviousGameBegin)) != null;

    public void Prepare()
    {
        slot.EventLog ??= DialogLogFiles.NewLog(slot.FileName);
        if (!season.PreviousGameData || HasPreviousGameData)
            return;

        if (_editor.FindFirst(_ => true) == null)
        {
            _editor.Append(id => EventLogEvent.ForSymbol(id, PreviousGameBegin, LocalSave));
            _editor.Append(id => EventLogEvent.ForSymbol(id, PreviousGameEnd, LocalSave));
            return;
        }

        _editor.InsertBefore(_editor.FindFirst(_ => true), id => EventLogEvent.ForSymbol(id, PreviousGameEnd, LocalSave));
        _editor.InsertBefore(_editor.FindFirst(entry => entry.Has(PreviousGameEnd)), id => EventLogEvent.ForSymbol(id, PreviousGameBegin, LocalSave));
    }

    public void Replace(IReadOnlyCollection<ulong> removed, IReadOnlyList<ulong> added, int episode)
    {
        var pending = new Queue<ulong>(added);
        foreach (var page in _editor.Log.Pages.ToList())
        {
            foreach (var entry in page.Events.Where(entry => entry.DialogNode is { } node && removed.Contains(node)).ToList())
            {
                if (pending.Count == 0)
                    break;

                entry.SetDialogNode(pending.Dequeue());
                _editor.Log.MarkModified(page);
            }
        }

        _editor.RemoveNodes(removed);
        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            _editor.InsertBefore(Anchor(episode), id => EventLogEvent.ForDialogNode(id, node));
        }
    }

    public void ReplacePreviousGameData(IEnumerable<ulong> nodes)
    {
        Prepare();
        var inside = false;
        var old = new HashSet<ulong>();
        foreach (var entry in _editor.Log.Events)
        {
            if (entry.Has(PreviousGameBegin))
                inside = true;
            else if (entry.Has(PreviousGameEnd))
                break;
            else if (inside && entry.DialogNode is { } node)
                old.Add(node);
        }

        _editor.RemoveNodes(old);
        _editor.InsertManyBefore(
            _editor.FindFirst(entry => entry.Has(PreviousGameEnd))!.Value,
            [.. nodes.Distinct().Select(node => (Func<uint, EventLogEvent>)(id => EventLogEvent.ForDialogNode(id, node)))]);
    }

    public void TruncateFromEpisode(int episode)
    {
        if (_editor.FindFirst(entry => entry.Number(EventLogEventTypes.BeginEpisode) >= episode) is { } position)
            _editor.TruncateFrom(position);
    }

    public void FinishEpisode(int episode)
    {
        BeginEpisode(episode);
        if (_editor.FindFirst(entry => entry.Number(EventLogEventTypes.EndEpisode) == episode) == null)
            _editor.Append(id => EventLogEvent.ForNumber(id, EventLogEventTypes.EndEpisode, episode));
    }

    public void BeginEpisode(int episode)
    {
        if (_editor.FindFirst(entry => entry.Number(EventLogEventTypes.BeginEpisode) == episode) == null)
            _editor.Append(id => EventLogEvent.ForNumber(id, EventLogEventTypes.BeginEpisode, episode));
    }

    public void AppendSaveSerial(int serial) => _editor.Append(id => EventLogEvent.ForSaveSerial(id, serial));

    private (EventLogPage Page, int Index)? Anchor(int episode)
    {
        if (episode <= 0)
        {
            Prepare();
            return _editor.FindFirst(entry => entry.Has(PreviousGameEnd));
        }

        var serial = slot.Metadata?.GetInt(SlotMetadataKeys.LatestSerial) ?? 0;
        return _editor.FindFirst(entry => entry.Number(EventLogEventTypes.BeginEpisode) > episode)
            ?? _editor.FindLast(entry => entry.Number(EventLogEventTypes.EndEpisode) == episode)
            ?? (serial > 0 ? _editor.FindLast(entry => entry.SaveSerial == serial) : null);
    }
}
