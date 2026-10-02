using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.ExtractChapters.Scenes;

public sealed class SceneDialogReader(IEnumerable<string> dialogNames)
{
    private static readonly ulong[] DialogKeys =
    [
        TelltaleCrc64.Compute("Dialog Agent - File Primary"),
        TelltaleCrc64.Compute("Dialog Agent - File Secondary"),
    ];

    private readonly Dictionary<ulong, string> _dialogs = dialogNames.ToDictionary(TelltaleCrc64.Compute);

    public List<string> Read(string path)
    {
        var data = MetaStreamParser.Parse(File.ReadAllBytes(path))?.Default ?? [];
        var found = new List<string>();
        for (var offset = 0; offset + 16 <= data.Length; offset++)
        {
            if (!DialogKeys.Contains(Bytes.U64(data, offset)))
                continue;

            if (_dialogs.TryGetValue(Bytes.U64(data, offset + 8), out var dialog) && !found.Contains(dialog))
                found.Add(dialog);
        }

        return found;
    }
}
