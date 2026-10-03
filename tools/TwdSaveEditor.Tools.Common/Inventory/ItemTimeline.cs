namespace TwdSaveEditor.Tools.Common.Inventory;

public sealed class ItemTimeline(IReadOnlyList<TimelinePoint> points, IReadOnlyList<ItemChange> changes, bool strict = false)
{
    private const int EpisodeStart = -1;

    public bool Held(string item, int position, bool fromStart)
    {
        var now = 2 * position;
        var timed = changes.Where(change => change.Item == item)
            .Select(change => (Change: change, Times: Times(change)))
            .Where(entry => entry.Times.Count > 0)
            .ToList();

        return strict ? HeldForCertain(item, now, fromStart, timed) : HeldAtMost(item, now, fromStart, timed);
    }

    private bool HeldAtMost(string item, int now, bool fromStart, List<(ItemChange Change, List<int> Times)> timed)
    {
        var adds = timed.Where(entry => !entry.Change.Removes).Select(entry => entry.Times[0]).ToList();
        var earlier = adds.Where(time => time < now).Concat(fromStart ? [EpisodeStart] : []).ToList();
        if (earlier.Count == 0)
        {
            return false;
        }

        var added = earlier.Max();
        var next = adds.Where(time => time >= now).DefaultIfEmpty(int.MaxValue).Min();
        var removals = timed.Where(entry => entry.Change.Removes)
            .Select(entry => (entry.Change, Time: entry.Times[^1]))
            .Where(entry => entry.Time >= added && entry.Time < next)
            .ToList();
        if (removals.Count == 0)
        {
            return true;
        }

        var last = removals.MaxBy(entry => entry.Time);
        return last.Time >= now || HandedOutAgain(item, last.Change, last.Time, timed);
    }

    private bool HeldForCertain(string item, int now, bool fromStart, List<(ItemChange Change, List<int> Times)> timed)
    {
        var passed = timed.Where(entry => !entry.Change.Removes && entry.Times[^1] < now).ToList();
        if (passed.Count == 0 && !fromStart)
        {
            return false;
        }

        var start = fromStart ? EpisodeStart : passed.Min(entry => entry.Times[0]);
        var removals = timed.Where(entry => entry.Change.Removes)
            .SelectMany(entry => entry.Times.Select(time => (entry.Change, Time: time)))
            .Where(entry => entry.Time >= start && entry.Time < now)
            .ToList();
        if (removals.Count == 0)
        {
            return true;
        }

        var first = removals.MinBy(entry => entry.Time);
        return HandedOutAgain(item, first.Change, first.Time, timed);
    }

    private bool HandedOutAgain(string item, ItemChange removal, int time, List<(ItemChange Change, List<int> Times)> timed) =>
        timed.Any(entry => !entry.Change.Removes && entry.Change.Scripts.Intersect(removal.Scripts, StringComparer.OrdinalIgnoreCase).Any())
        && Enumerable.Range(0, points.Count).Any(index => 2 * index > time && points[index].Setup.Contains(item));

    private List<int> Times(ItemChange change)
    {
        if (change.ChapterId != null)
        {
            var first = Find(point => point.ChapterId == change.ChapterId && Uses(change, point));
            if (first < 0)
            {
                first = Find(point => point.ChapterId == change.ChapterId);
            }

            if (first >= 0)
            {
                return [2 * first + 1];
            }
        }

        var visits = new List<int>();
        for (var index = 0; index < points.Count; index++)
        {
            if (Uses(change, points[index]) && (index + 1 == points.Count || !Uses(change, points[index + 1])))
            {
                visits.Add(2 * index + 1);
            }
        }

        return visits;
    }

    private int Find(Func<TimelinePoint, bool> match)
    {
        for (var index = 0; index < points.Count; index++)
        {
            if (match(points[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Uses(ItemChange change, TimelinePoint point) =>
        change.Scripts.Contains(point.Script, StringComparer.OrdinalIgnoreCase);
}
