using System.Text;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.SaveGames;

public static class SaveGameCodec
{
    private const int DebugBytesPerSymbol = 4;
    private const int SymbolsPerAgent = 4;

    private const uint SaveGameVersion = 0xF34230F8;
    private const uint AgentInfoVersion = 0x5C9F9D09;
    private const uint SymbolVersion = 0xB539B0FF;

    public static SaveGameFile Read(byte[] file)
    {
        var content = MetaStreamCodec.Read(file);
        using var reader = new BinaryReader(new MemoryStream(content.Default));

        reader.ReadUInt32();
        var script = Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadInt32()));

        reader.ReadUInt32();
        var agents = new List<byte[]>();
        for (var count = reader.ReadUInt32(); count > 0; count--)
            agents.Add(reader.ReadBytes(SaveGameFile.AgentSize));

        var names = ReadSymbols(reader);
        var sets = ReadSymbols(reader);

        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("default.save has unread data.");

        return new SaveGameFile
        {
            LuaDoFile = script,
            Agents = agents,
            RuntimePropertyNames = names,
            EnabledDynamicSets = sets,
            VersionEntries = content.Header.VersionEntries,
        };
    }

    public static byte[] Write(SaveGameFile save)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            var script = Encoding.Latin1.GetBytes(save.LuaDoFile);
            writer.Write((uint)(8 + script.Length));
            writer.Write(script.Length);
            writer.Write(script);

            writer.Write((uint)(8 + save.Agents.Count * SaveGameFile.AgentSize));
            writer.Write(save.Agents.Count);
            foreach (var agent in save.Agents)
                writer.Write(agent);

            WriteSymbols(writer, save.RuntimePropertyNames);
            WriteSymbols(writer, save.EnabledDynamicSets);
        }

        var symbols = save.Agents.Count * SymbolsPerAgent + save.RuntimePropertyNames.Count + save.EnabledDynamicSets.Count;
        var header = new MetaStreamHeader
        {
            Magic = MetaStreamHeader.MagicMsv6,
            VersionEntries = save.VersionEntries.Count > 0 ? save.VersionEntries : DefaultVersionEntries(),
        };

        return MetaStreamCodec.Write(new MetaStreamContent(header, stream.ToArray(), new byte[symbols * DebugBytesPerSymbol], []));
    }

    private static List<VersionEntry> DefaultVersionEntries() =>
    [
        new(TelltaleTypes.SaveGame, SaveGameVersion),
        new(TelltaleTypes.SaveGameAgentInfo, AgentInfoVersion),
        new(TelltaleTypes.Symbol, SymbolVersion),
    ];

    private static List<ulong> ReadSymbols(BinaryReader reader)
    {
        reader.ReadUInt32();
        var symbols = new List<ulong>();
        for (var count = reader.ReadUInt32(); count > 0; count--)
            symbols.Add(reader.ReadUInt64());

        return symbols;
    }

    private static void WriteSymbols(BinaryWriter writer, List<ulong> symbols)
    {
        writer.Write((uint)(8 + symbols.Count * sizeof(ulong)));
        writer.Write(symbols.Count);
        foreach (var symbol in symbols.Order())
            writer.Write(symbol);
    }
}
