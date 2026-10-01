using System.Buffers.Binary;
using System.Text;
using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.CreateEndEpisodeEstore.Estores;

public static class EndEpisodeEstoreBuilder
{
    public const string Extension = ".estore";

    private const string TemplatePrefix = "menu_log_";

    private static readonly byte[] ButtonPress = Bytes.FromU64(TelltaleCrc64.Compute("Button Press"));
    private static readonly byte[] EndEpisode = Bytes.FromU64(TelltaleCrc64.Compute("End Episode"));

    public static bool HasEndEpisodeEvent(ReadOnlySpan<byte> estore) => Bytes.IndexOf(estore, EndEpisode) >= 0;

    public static byte[]? Build(string saveDirectory, string logName, TextWriter output)
    {
        var template = Directory.GetFiles(saveDirectory)
            .Where(path => Path.GetFileName(path).StartsWith(TemplatePrefix, StringComparison.Ordinal))
            .Where(path => path.EndsWith(Extension, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .LastOrDefault();

        if (template == null)
        {
            output.WriteLine("No menu_log template found!");
            return null;
        }

        var data = File.ReadAllBytes(template);

        var headerEnd = 20 + Bytes.U32(data, 16) * 12L;
        var defaultSize = MetaStreamParser.SectionSize(Bytes.U32(data, 4));
        var debugSize = MetaStreamParser.SectionSize(Bytes.U32(data, 8));
        var section = Bytes.Slice(data, headerEnd, headerEnd + defaultSize).ToArray();
        var debug = Bytes.Slice(data, headerEnd + defaultSize, headerEnd + defaultSize + debugSize);

        var position = Bytes.IndexOf(section, ButtonPress);
        if (position < 0)
        {
            output.WriteLine("Could not find Button Press event in template!");
            return null;
        }

        EndEpisode.CopyTo(section, position);
        output.WriteLine($"Replaced Button Press with End Episode at offset 0x{position:X}");

        var oldName = Encoding.ASCII.GetBytes(Path.GetFileNameWithoutExtension(template) + Extension);
        var newName = Encoding.ASCII.GetBytes(logName + Extension);

        var namePosition = Bytes.IndexOf(section, oldName);
        if (namePosition >= 0 && oldName.Length == newName.Length)
        {
            newName.CopyTo(section, namePosition);
            output.WriteLine($"Updated log name to {logName}");
        }
        else if (namePosition >= 0)
        {
            output.WriteLine($"WARNING: log name length mismatch ({oldName.Length} vs {newName.Length}), keeping original name");
        }

        var header = Bytes.Slice(data, 0, headerEnd).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)section.Length);

        return [.. header, .. section, .. debug];
    }
}
