using System.Globalization;
using System.Text;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.Common.Language;

public sealed class LanguageReader(MetaReader reader)
{
    public const string RootType = "LanguageDB";

    public Dictionary<long, string> Read(string path)
    {
        var document = reader.Read(File.ReadAllBytes(path), RootType);
        if (document?.Root is not MetaObject root || root.Find("mLanguageResources") is not MetaMap resources)
        {
            throw new MetaFormatException($"{Path.GetFileName(path)}: {document?.Error ?? "not a language database"}");
        }

        var texts = new Dictionary<long, string>();
        foreach (var (key, value) in resources.Entries)
        {
            if (key is MetaScalar id
                && value is MetaObject resource
                && resource.Find("mResolvedLocalData") is MetaObject local
                && local.Find("mText") is MetaScalar { Text: { } text })
            {
                texts[Convert.ToInt64(id.Value, CultureInfo.InvariantCulture)] = TextFormat.DecodeUtf8(Encoding.Latin1.GetBytes(text));
            }
        }

        return texts;
    }
}
