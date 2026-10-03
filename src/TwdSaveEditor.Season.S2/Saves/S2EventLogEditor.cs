using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.S2.Decisions;

namespace TwdSaveEditor.Season.S2.Saves;

public sealed class S2EventLogEditor(SaveSlot slot)
{
    private readonly DialogLogEditor _editor = new(slot);

    private EventLog Log => slot.EventLog ?? throw new InvalidOperationException("The save has no event log.");

    public static ulong NodeSymbol(string node) => TelltaleHash.ComputeCrc64("{" + node + "}");

    public HashSet<ulong> Nodes() => _editor.Nodes();

    public string? GetValue(S2Decision decision) => GetValue(decision, Nodes());

    public static string? GetValue(S2Decision decision, IReadOnlySet<ulong> present) =>
        (FindSeen(decision, present) ?? decision.Options.FirstOrDefault(option => option.Nodes.Count == 0))?.Value;

    public S2DecisionOption? FindSeen(S2Decision decision) => slot.EventLog == null ? null : FindSeen(decision, Nodes());

    public static S2DecisionOption? FindSeen(S2Decision decision, IReadOnlySet<ulong> present) =>
        decision.Options.FirstOrDefault(option => option.Nodes.Any(node => present.Contains(NodeSymbol(node))));

    public void Clear(S2Decision decision)
    {
        var nodes = decision.Options.SelectMany(option => option.Nodes).Select(NodeSymbol).ToHashSet();
        foreach (var page in Log.Pages.ToList())
        {
            if (page.Events.RemoveAll(entry => entry.DialogNode is { } node && nodes.Contains(node)) > 0)
            {
                Log.MarkModified(page);
            }
        }
    }

    public void TruncateAfterSerial(long serial)
    {
        var anchor = _editor.FindLast(entry => entry.SaveSerial == serial);
        _editor.TruncateFrom(anchor == null ? null : (anchor.Value.Page, anchor.Value.Index + 1));
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
                {
                    continue;
                }

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
        {
            Insert(NodeSymbol(target.Nodes[0]), decision.Episode);
        }

        foreach (var required in decision.Requires.Select(NodeSymbol))
        {
            if (!Log.Events.Any(entry => entry.DialogNode == required))
            {
                Insert(required, decision.Episode);
            }
        }
    }

    private void Insert(ulong node, int episode) =>
        _editor.InsertBefore(FindAnchor(episode), id => EventLogEvent.ForDialogNode(id, node));

    private (EventLogPage Page, int Index)? FindAnchor(int episode)
    {
        foreach (var serial in AnchorSerials(episode))
        {
            foreach (var page in Log.Pages)
            {
                var index = page.Events.FindLastIndex(entry => entry.SaveSerial == serial);
                if (index >= 0)
                {
                    return (page, index);
                }
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

    public void AppendSaveSerial(int serial) => _editor.Append(id => EventLogEvent.ForSaveSerial(id, serial));
}
