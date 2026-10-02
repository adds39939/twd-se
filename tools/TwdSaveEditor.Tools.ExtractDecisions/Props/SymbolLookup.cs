using System.Globalization;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Names;

namespace TwdSaveEditor.Tools.ExtractDecisions.Props;

public sealed class SymbolLookup
{
    private const int MaxIndex = 64;

    private static readonly Dictionary<ulong, int> Indexes = Enumerable.Range(0, MaxIndex)
        .ToDictionary(index => TelltaleCrc64.Compute(index.ToString(CultureInfo.InvariantCulture)));

    private readonly SymbolNames _names = SymbolNames.LoadDefault();

    public static int Index(ulong symbol) => Indexes.GetValueOrDefault(symbol, MaxIndex);

    public string Resolve(ulong symbol) =>
        _names.Find(symbol) ?? throw new InvalidDataException($"No name is known for the symbol {symbol:X16}.");
}
