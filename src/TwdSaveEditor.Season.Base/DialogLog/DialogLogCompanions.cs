using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using GameLog = TwdSaveEditor.Core.Model.EventLog;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.DialogLog;

public static class DialogLogCompanions
{
    public static IReadOnlyList<string> Find(string bundleFileName, IEnumerable<string> directoryFileNames)
    {
        if (!DialogLogFiles.IsSlotBundle(bundleFileName))
            return [];

        var storage = DialogLogFiles.StorageName(bundleFileName);
        return directoryFileNames
            .Where(name => name.Equals(storage, StringComparison.OrdinalIgnoreCase)
                || DialogLogFiles.IsPage(bundleFileName, name)
                || DialogLogFiles.IsSave(bundleFileName, name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void Attach(SaveSlot slot, IReadOnlyList<CompanionFile> files, string seasonKey)
    {
        slot.Checkpoints.Clear();
        slot.EventLog = null;

        var storage = files.FirstOrDefault(file => file.Name.EndsWith(DialogLogFiles.StorageSuffix, StringComparison.OrdinalIgnoreCase));
        if (storage != null && TryRead(() => EventLogCodec.ReadStorage(storage.Data)) is { } parsed)
        {
            slot.EventLog = new GameLog(storage.Name, parsed);
            var listed = parsed.Pages.Select(entry => entry.PageSymbol).ToHashSet();
            foreach (var file in files.Where(file => file.Name.EndsWith(DialogLogFiles.PageExtension, StringComparison.OrdinalIgnoreCase)
                && listed.Contains(TelltaleHash.ComputeCrc64(file.Name))))
            {
                if (TryRead(() => EventLogCodec.ReadPage(file.Data)) is { } page)
                    slot.EventLog.PageFiles.Add(new EventLogPageFile(file.Name, page));
            }
        }

        foreach (var file in files.Where(file => file.Name.EndsWith(DialogLogFiles.BundleExtension, StringComparison.OrdinalIgnoreCase)))
        {
            if (TryRead(() => BundleReader.Read(file.Data, file.Name)) is { } save)
            {
                save.DetectedSeasonKey = seasonKey;
                slot.Checkpoints.Add(save);
            }
        }
    }

    public static IReadOnlyList<CompanionFile> Build(SaveSlot slot)
    {
        var files = new List<CompanionFile>();
        if (slot.EventLog is { } log)
        {
            if (log.StorageModified)
                files.Add(new CompanionFile(log.StorageName, EventLogCodec.WriteStorage(log.Storage)));

            files.AddRange(log.PageFiles.Where(page => page.Modified).Select(page => new CompanionFile(page.Name, EventLogCodec.WritePage(page.Page))));
        }

        files.AddRange(slot.Checkpoints.Where(save => save.Modified).Select(save => new CompanionFile(save.FileName, BundleWriter.Write(save))));
        return files;
    }

    public static IReadOnlyList<string> Names(SaveSlot slot)
    {
        if (!DialogLogFiles.IsSlotBundle(slot.FileName))
            return [];

        var names = slot.Checkpoints.Select(save => save.FileName).ToList();
        if (slot.EventLog is { } log)
            names.AddRange(log.PageFiles.Select(page => page.Name).Prepend(log.StorageName));

        return names;
    }

    private static T? TryRead<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }
}
