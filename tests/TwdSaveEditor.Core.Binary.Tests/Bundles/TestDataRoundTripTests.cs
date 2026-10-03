using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Tests.Common.Data;

namespace TwdSaveEditor.Core.Binary.Tests.Bundles;

public class TestDataRoundTripTests
{
    public static TheoryData<string, string> Files(string extension)
    {
        var files = new TheoryData<string, string>();
        foreach (var season in Directory.GetDirectories(TestDataHelper.GetSeasonDir(string.Empty)).Select(Path.GetFileName).Order())
        {
            foreach (var path in Directory.GetFiles(TestDataHelper.GetSeasonDir(season!), "*" + extension).Order())
            {
                files.Add(season!, Path.GetFileName(path));
            }
        }

        return files;
    }

    [Theory]
    [MemberData(nameof(Files), ".bundle")]
    public void EveryPropertySetAndSaveGame_RewritesByteForByte(string season, string fileName)
    {
        var slot = BundleReader.Read(TestDataHelper.GetPath(season, fileName));

        foreach (var file in slot.Files.Where(file => file.TypeSymbol == TelltaleTypes.PropertySet))
        {
            var section = MetaStreamCodec.Read(file.Data).Default;
            Assert.Equal(section, new PropertySetWriter().Write(new PropertySetReader().Read(section)));
        }

        foreach (var file in slot.Files.Where(file => file.TypeSymbol == TelltaleTypes.SaveGame))
        {
            Assert.Equal(file.Data, SaveGameCodec.Write(SaveGameCodec.Read(file.Data)));
        }
    }

    [Theory]
    [MemberData(nameof(Files), ".estore")]
    [MemberData(nameof(Files), ".epage")]
    public void EveryEventLogFile_RewritesTheSameContent(string season, string fileName)
    {
        var data = File.ReadAllBytes(TestDataHelper.GetPath(season, fileName));
        var written = fileName.EndsWith(".estore")
            ? EventLogCodec.WriteStorage(EventLogCodec.ReadStorage(data))
            : EventLogCodec.WritePage(EventLogCodec.ReadPage(data));

        Assert.Equal(MetaStreamCodec.Read(data).Default, MetaStreamCodec.Read(written).Default);
    }
}
