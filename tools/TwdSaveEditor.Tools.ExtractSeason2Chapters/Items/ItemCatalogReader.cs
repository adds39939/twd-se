using System.Globalization;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Language;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Scripts;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Items;

public sealed partial class ItemCatalogReader(MetaReader reader, DialogLoader loader)
{
    public const string TextDialog = "ui_episode.dlog";
    public const string TextDatabase = "ui_episode_english.landb";

    private const string NameKey = "Item - Name";
    private const string TextNodePrefix = "item_";
    private const string TextNodeKind = "Text";

    public List<Item> Read(string directory)
    {
        var dialogPath = Path.Combine(directory, TextDialog);
        var databasePath = Path.Combine(directory, TextDatabase);
        var dialog = File.Exists(dialogPath) ? loader.Load(dialogPath) : null;
        var texts = File.Exists(databasePath) ? new LanguageReader(reader).Read(databasePath) : [];

        var items = new List<Item>();
        foreach (var path in Directory.EnumerateFiles(directory, InventoryCalls.ItemPrefix + "*.prop").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            var key = (reader.ReadPropertySet(File.ReadAllBytes(path))?.Find(NameKey) as MetaScalar)?.Text ?? id[InventoryCalls.ItemPrefix.Length..];
            items.Add(new Item(id, key, DisplayName(dialog, texts, key) ?? Words().Replace(key, " $1").Trim()));
        }

        return items;
    }

    private static string? DisplayName(DialogFile? dialog, Dictionary<long, string> texts, string key)
    {
        var name = TelltaleCrc64.Compute(TextNodePrefix + key);
        var node = dialog?.Nodes.Values.FirstOrDefault(candidate => candidate.Name == name);
        for (var steps = 0; node != null && steps < dialog!.Nodes.Count; steps++, node = dialog.Find(node.Next))
        {
            if (node.Kind == TextNodeKind
                && node.Source.Find("mLangResProxy") is MetaObject proxy
                && proxy.Find("mID") is MetaScalar id
                && texts.TryGetValue(Convert.ToInt64(id.Value, CultureInfo.InvariantCulture), out var text))
            {
                return text;
            }
        }

        return null;
    }

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex Words();
}
