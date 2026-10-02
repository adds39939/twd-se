using TwdSaveEditor.Tools.Common.Inventory;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Items;

public sealed class CarriedItems(IReadOnlyList<ResumePoint> points, IReadOnlyList<ItemChange> changes, ILookup<string, string> setups)
{
    private readonly ItemTimeline _timeline = new(
        [
            .. points.Select((point, index) => new TimelinePoint(
                point.Id,
                point.Script,
                point.ChapterId,
                index == 0 || !points[index - 1].Script.Equals(point.Script, StringComparison.OrdinalIgnoreCase) ? [.. setups[point.Script]] : [])),
        ],
        changes);

    public ChapterItems At(int position, IReadOnlyList<Item> items, IReadOnlyCollection<string> starting)
    {
        var carried = items.Select(item => item.Id).Where(item => _timeline.Held(item, position, false)).ToList();
        var fromStart = items.Select(item => item.Id)
            .Where(item => starting.Contains(item) && !carried.Contains(item) && _timeline.Held(item, position, true))
            .ToList();

        return new ChapterItems(points[position].Id, carried, fromStart);
    }
}
