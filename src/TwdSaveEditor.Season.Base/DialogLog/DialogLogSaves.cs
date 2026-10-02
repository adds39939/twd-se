using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Base.DialogLog;

public static class DialogLogSaves
{
    public static PropertySet RuntimeProperties(SaveSlot save, ulong name, string unreadable)
    {
        if (save.FindFile(name) is { } existing)
            return BundleReader.TryParseProperties(existing) ? existing.Properties! : throw new InvalidOperationException(unreadable);

        var properties = DialogLogFiles.NewRuntimeProperties();
        var files = save.Files;
        var position = files.FindIndex(file => file.Name.Length == 0 && file.NameSymbol > name);
        files.Insert(position < 0 ? files.Count : position, new BundleFileEntry
        {
            NameField = new byte[BundleFileEntry.NameFieldSize],
            NameSymbol = name,
            TypeSymbol = TelltaleTypes.PropertySet,
            Data = [],
            Properties = properties,
        });

        if (save.FindFile(BundleFileNames.SaveGame) is { } saveGame)
        {
            var state = SaveGameCodec.Read(saveGame.Data);
            if (!state.RuntimePropertyNames.Contains(name))
            {
                var index = state.RuntimePropertyNames.FindIndex(existingName => existingName > name);
                state.RuntimePropertyNames.Insert(index < 0 ? state.RuntimePropertyNames.Count : index, name);
                saveGame.Data = SaveGameCodec.Write(state);
            }
        }

        return properties;
    }
}
