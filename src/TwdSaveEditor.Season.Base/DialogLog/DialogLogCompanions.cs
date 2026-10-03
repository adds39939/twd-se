using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Core.Serialization;
using GameLog = TwdSaveEditor.Core.Model.EventLog;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.DialogLog;

public sealed class DialogLogCompanions(ISaveBundleSerializer serializer) : IDialogLogCompanions
{
    public IReadOnlyList<string> Find(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        if (!DialogLogFiles.IsSlotBundle(bundleFileName))
        {
            return [];
        }

        var storage = DialogLogFiles.StorageName(bundleFileName);
        return directoryFileNames
            .Where(name => name.Equals(storage, StringComparison.OrdinalIgnoreCase)
                || DialogLogFiles.IsPage(bundleFileName, name)
                || DialogLogFiles.IsSave(bundleFileName, name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Attach(SaveSlot slot, IReadOnlyList<CompanionFile> files, string seasonKey)
    {
        slot.Checkpoints.Clear();
        slot.EventLog = null;
        slot.EventLogDamaged = false;

        var storage = files.FirstOrDefault(file => file.Name.EndsWith(DialogLogFiles.StorageSuffix, StringComparison.OrdinalIgnoreCase));
        if (storage != null)
        {
            slot.EventLog = ReadLog(storage, files);
            slot.EventLogDamaged = slot.EventLog == null;
        }

        foreach (var file in files.Where(file => file.Name.EndsWith(DialogLogFiles.BundleExtension, StringComparison.OrdinalIgnoreCase)))
        {
            if (TryRead(() => serializer.Read(file.Data, file.Name)) is { } save)
            {
                save.DetectedSeasonKey = seasonKey;
                slot.Checkpoints.Add(save);
            }
        }
    }

    public IReadOnlyList<CompanionFile> Build(SaveSlot slot)
    {
        var files = new List<CompanionFile>();
        if (slot.EventLog is { } log)
        {
            if (log.StorageModified)
            {
                files.Add(new CompanionFile(log.StorageName, EventLogCodec.WriteStorage(log.Storage)));
            }

            files.AddRange(log.PageFiles.Where(page => page.Modified).Select(page => new CompanionFile(page.Name, EventLogCodec.WritePage(page.Page))));
        }

        files.AddRange(slot.Checkpoints.Where(save => save.Modified).Select(save => new CompanionFile(save.FileName, serializer.Write(save))));
        return files;
    }

    public IReadOnlyList<string> Names(SaveSlot slot)
    {
        if (!DialogLogFiles.IsSlotBundle(slot.FileName))
        {
            return [];
        }

        var names = slot.Checkpoints.Select(save => save.FileName).ToList();
        if (slot.EventLog is { } log)
        {
            names.AddRange(log.PageFiles.Select(page => page.Name).Prepend(log.StorageName));
        }

        return names;
    }

    private static GameLog? ReadLog(CompanionFile storage, IReadOnlyList<CompanionFile> files)
    {
        if (TryRead(() => EventLogCodec.ReadStorage(storage.Data)) is not { } parsed)
        {
            return null;
        }

        var log = new GameLog(storage.Name, parsed);
        var listed = parsed.Pages.Select(entry => entry.PageSymbol).ToHashSet();
        foreach (var file in files.Where(file => file.Name.EndsWith(DialogLogFiles.PageExtension, StringComparison.OrdinalIgnoreCase)
            && listed.Contains(TelltaleHash.ComputeCrc64(file.Name))))
        {
            if (TryRead(() => EventLogCodec.ReadPage(file.Data)) is not { } page)
            {
                return null;
            }

            log.PageFiles.Add(new EventLogPageFile(file.Name, page));
        }

        return log;
    }

    private static T? TryRead<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
