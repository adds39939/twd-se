using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Items;

public sealed class CarriedItems(IReadOnlyList<ResumePoint> points, IReadOnlyList<ItemChange> changes, ILookup<string, string> setups)
{
    private const int EpisodeStart = -1;

    public ChapterItems At(int position, IReadOnlyList<Item> items, IReadOnlyCollection<string> starting)
    {
        var carried = items.Select(item => item.Id).Where(item => Held(item, position, false)).ToList();
        var fromStart = items.Select(item => item.Id)
            .Where(item => starting.Contains(item) && !carried.Contains(item) && Held(item, position, true))
            .ToList();

        return new ChapterItems(points[position].Id, carried, fromStart);
    }

    private bool Held(string item, int position, bool fromStart)
    {
        var now = 2 * position;
        var timed = changes.Where(change => change.Item == item)
            .Select(change => (Change: change, Time: Time(change)))
            .Where(entry => entry.Time != null)
            .Select(entry => (entry.Change, Time: entry.Time!.Value))
            .ToList();

        var adds = timed.Where(entry => !entry.Change.Removes).Select(entry => entry.Time).ToList();
        var earlier = adds.Where(time => time < now).Concat(fromStart ? [EpisodeStart] : []).ToList();
        if (earlier.Count == 0)
            return false;

        var added = earlier.Max();
        var next = adds.Where(time => time >= now).DefaultIfEmpty(int.MaxValue).Min();
        var removals = timed.Where(entry => entry.Change.Removes && entry.Time >= added && entry.Time < next).ToList();
        if (removals.Count == 0)
            return true;

        var last = removals.MaxBy(entry => entry.Time);
        if (last.Time >= now)
            return true;

        var readded = timed.Any(entry => !entry.Change.Removes && entry.Change.Scripts.Intersect(last.Change.Scripts, StringComparer.OrdinalIgnoreCase).Any());
        return readded && Enumerable.Range(0, points.Count).Any(index =>
            2 * index > last.Time
            && (index == 0 || !points[index - 1].Script.Equals(points[index].Script, StringComparison.OrdinalIgnoreCase))
            && setups[points[index].Script].Contains(item));
    }

    private int? Time(ItemChange change)
    {
        if (change.ChapterId != null)
        {
            var first = Find(point => point.ChapterId == change.ChapterId && Uses(change, point));
            if (first < 0)
                first = Find(point => point.ChapterId == change.ChapterId);
            if (first >= 0)
                return 2 * first + 1;
        }

        var visits = new List<int>();
        for (var index = 0; index < points.Count; index++)
        {
            if (Uses(change, points[index]) && (index + 1 == points.Count || !Uses(change, points[index + 1])))
                visits.Add(index);
        }

        return visits.Count == 0 ? null : 2 * (change.Removes ? visits[^1] : visits[0]) + 1;
    }

    private int Find(Func<ResumePoint, bool> match)
    {
        for (var index = 0; index < points.Count; index++)
        {
            if (match(points[index]))
                return index;
        }

        return -1;
    }

    private static bool Uses(ItemChange change, ResumePoint point) =>
        change.Scripts.Contains(point.Script, StringComparer.OrdinalIgnoreCase);
}
