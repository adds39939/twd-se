using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Tests.Common.Seasons;
using TwdSaveEditor.Tests.Common.Saves;

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
    public void LoadFromJson_ArrayFormat()
    {
        var db = new PropertyNameDb();
        db.LoadFromJson("""["mHealth", "mStamina", "mbAlive"]""");
        Assert.Equal(3, db.Count);
        Assert.Equal("mHealth", db.Resolve(Symbol.FromString("mHealth")));
    }

    [Fact]
    public void CreateDefault_HasTypeNames()
    {
        var db = PropertyNameDb.CreateDefault();
        Assert.Equal("bool", db.Resolve(Symbol.FromString("bool")));
        Assert.Equal("int32", db.Resolve(Symbol.FromString("int32")));
        Assert.Equal("String", db.Resolve(Symbol.FromString("String")));
        Assert.Equal("PropertySet", db.Resolve(Symbol.FromString("PropertySet")));
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
