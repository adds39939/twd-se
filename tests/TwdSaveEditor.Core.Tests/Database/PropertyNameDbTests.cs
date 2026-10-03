using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Tests.Common.Seasons;

namespace TwdSaveEditor.Core.Tests.Database;

public class PropertyNameDbTests
{
    [Fact]
    public void Register_And_Resolve()
    {
        var db = new PropertyNameDb();
        db.Register("mHealth");
        var sym = Symbol.FromString("mHealth");
        Assert.Equal("mHealth", db.Resolve(sym));
    }

    [Fact]
    public void ResolveOrHex_UnknownSymbol_ReturnsHex()
    {
        var db = new PropertyNameDb();
        var sym = new Symbol(0xDEADBEEFCAFEBABE);
        Assert.Equal("0xDEADBEEFCAFEBABE", db.ResolveOrHex(sym));
    }

    [Fact]
    public void CreateDefault_HasTheSaveMetadataKeys()
    {
        var db = PropertyNameDb.CreateDefault();
        Assert.Equal(SlotMetadataKeys.LatestSave, db.Resolve(Symbol.FromString(SlotMetadataKeys.LatestSave)));
        Assert.Equal(SaveMetadataKeys.ChapterId, db.Resolve(Symbol.FromString(SaveMetadataKeys.ChapterId)));
    }

    [Fact]
    public void CreateDefault_RegistersAdditionalNames()
    {
        var choiceKeys = TestSeasons.AllChoices.Select(c => c.ChoiceKey).ToList();
        var db = PropertyNameDb.CreateDefault(choiceKeys);

        Assert.NotEmpty(choiceKeys);
        Assert.All(choiceKeys, key => Assert.Equal(key, db.Resolve(Symbol.FromString(key))));
        Assert.Null(PropertyNameDb.CreateDefault().Resolve(Symbol.FromString(choiceKeys[0])));
    }
}
