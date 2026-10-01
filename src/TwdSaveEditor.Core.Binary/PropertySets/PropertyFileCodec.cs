using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.PropertySets;

public static class PropertyFileCodec
{
    private const int DebugBytesPerSymbol = 4;

    private const uint PropertySetVersion = 0x21F2BCC9;
    private const uint FlagsVersion = 0x0527D6BF;
    private const uint SymbolVersion = 0xB539B0FF;

    public static PropertySet Read(byte[] file) =>
        new PropertySetReader().Read(MetaStreamCodec.Read(file).Default);

    public static byte[] Write(byte[] originalFile, PropertySet properties)
    {
        var data = new PropertySetWriter().Write(properties);
        var symbolCount = PropertySetSymbols.Count(properties);

        if (originalFile.Length == 0)
            return MetaStreamCodec.Write(new MetaStreamContent(CreateHeader(symbolCount), data, new byte[symbolCount * DebugBytesPerSymbol], []));

        var original = MetaStreamCodec.Read(originalFile);
        if (original.Default.AsSpan().SequenceEqual(data))
            return originalFile;

        if (symbolCount > 0 && original.Header.VersionEntries.All(entry => entry.TypeCrc != TelltaleTypes.Symbol))
            original.Header.VersionEntries.Add(new VersionEntry(TelltaleTypes.Symbol, SymbolVersion));

        return MetaStreamCodec.Write(original with { Default = data, Debug = ResizeDebug(original, symbolCount) });
    }

    private static byte[] ResizeDebug(MetaStreamContent original, int symbolCount)
    {
        var originalSymbolCount = CountOriginalSymbols(original.Default);
        if (original.Debug.Length == 0 && originalSymbolCount != 0)
            return [];

        var size = original.Debug.Length + (symbolCount - originalSymbolCount) * DebugBytesPerSymbol;
        var debug = new byte[Math.Max(size, 0)];
        original.Debug.AsSpan(0, Math.Min(original.Debug.Length, debug.Length)).CopyTo(debug);
        return debug;
    }

    private static int CountOriginalSymbols(byte[] data)
    {
        try
        {
            return PropertySetSymbols.Count(new PropertySetReader().Read(data));
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException)
        {
            return 0;
        }
    }

    private static MetaStreamHeader CreateHeader(int symbolCount)
    {
        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries =
            [
                new VersionEntry(TelltaleTypes.PropertySet, PropertySetVersion),
                new VersionEntry(TelltaleTypes.Flags, FlagsVersion),
            ],
        };

        if (symbolCount > 0)
            header.VersionEntries.Add(new VersionEntry(TelltaleTypes.Symbol, SymbolVersion));

        return header;
    }
}
