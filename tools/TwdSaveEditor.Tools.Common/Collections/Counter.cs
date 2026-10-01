namespace TwdSaveEditor.Tools.Common.Collections;

public sealed class Counter<T> where T : notnull
{
    private readonly OrderedDictionary<T, int> _counts = [];

    public int Count => _counts.Count;

    public int this[T key] => _counts.GetValueOrDefault(key);

    public void Add(T key, int amount = 1) => _counts[key] = this[key] + amount;

    public void AddRange(Counter<T> other)
    {
        foreach (var (key, count) in other._counts)
            Add(key, count);
    }

    public List<KeyValuePair<T, int>> MostCommon(int? limit = null)
    {
        var ordered = _counts.OrderByDescending(pair => pair.Value);
        return (limit is { } take ? ordered.Take(take) : ordered).ToList();
    }
}
