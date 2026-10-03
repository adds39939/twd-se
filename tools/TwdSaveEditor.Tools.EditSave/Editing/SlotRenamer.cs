using System.Text.RegularExpressions;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Tools.EditSave.Editing;

public sealed partial class SlotRenamer(int slotNumber)
{
    public string Rename(string name) => SlotName().Replace(name, match => match.Groups[1].Value.ToLowerInvariant() + slotNumber);

    public void Apply(SaveSlot slot)
    {
        if (slot.Metadata?.GetString(SlotMetadataKeys.LatestSave) is { Length: > 0 } latest)
        {
            slot.Metadata.SetString(SlotMetadataKeys.LatestSave, Rename(latest));
        }

        if (slot.EventLog is not { } log)
        {
            return;
        }

        var storage = log.Storage;
        storage.Name = Rename(storage.Name);
        var renamed = new EventLog(Rename(log.StorageName), storage) { StorageModified = true };
        storage.Pages = [.. storage.Pages.Select(entry =>
        {
            var file = log.FindPageFile(entry.PageSymbol);
            return file == null ? entry : entry with { PageSymbol = TelltaleHash.ComputeCrc64(Rename(file.Name)) };
        })];

        foreach (var file in log.PageFiles)
        {
            if (file.Page.FlushedName.Length > 0)
            {
                file.Page.FlushedName = Rename(file.Page.FlushedName);
            }

            renamed.PageFiles.Add(new EventLogPageFile(Rename(file.Name), file.Page) { Modified = true });
        }

        slot.EventLog = renamed;
    }

    [GeneratedRegex(@"(saveslot)\d+", RegexOptions.IgnoreCase)]
    private static partial Regex SlotName();
}
