using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Unit;

/// <summary>
/// Tests that metadata int/string/bool properties can be read and modified in-place.
/// </summary>
public class MetadataEditTests
{
    private static PropertySet CreateMetadata()
        => SaveSlotFactory.CreateBlankMetadata("WalkingDead101", "test.bundle");

    [Fact]
    public void PlaytimeProperty_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

        // Hash 0x7C725227A47FD1BA is the first int property (playtime / chapter count)
        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x7C725227A47FD1BA);

        Assert.NotNull(prop);
        Assert.IsType<IntValue>(prop.Value);

        var intVal = (IntValue)prop.Value;
        var original = intVal.Value;
        Assert.Equal(1, original); // default from CreateBlankMetadata

        intVal.Value = 12345;
        Assert.Equal(12345, ((IntValue)prop.Value).Value);
    }

    [Fact]
    public void EpisodeProgress_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

        // Hash 0xB218E7C003A67CE9 is the episode progress string
        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);

        Assert.NotNull(prop);
        Assert.IsType<StringValue>(prop.Value);

        var strVal = (StringValue)prop.Value;
        Assert.Equal("WalkingDead101", strVal.Value);

        strVal.Value = "WalkingDead205";
        Assert.Equal("WalkingDead205", ((StringValue)prop.Value).Value);
    }

    [Fact]
    public void AutosaveFile_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

        // Hash 0xF235E9FCE9562E01 is the autosave filename string
        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xF235E9FCE9562E01);

        Assert.NotNull(prop);
        Assert.IsType<StringValue>(prop.Value);

        var strVal = (StringValue)prop.Value;
        Assert.Equal("_test_autosave.bundle", strVal.Value);

        strVal.Value = "_custom_autosave.bundle";
        Assert.Equal("_custom_autosave.bundle", ((StringValue)prop.Value).Value);
    }

    [Fact]
    public void GameComplete_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

        // Hash 0x4F8338150CC8BCD6 is the bool property (game complete)
        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x4F8338150CC8BCD6);

        Assert.NotNull(prop);
        Assert.IsType<BoolValue>(prop.Value);

        var boolVal = (BoolValue)prop.Value;
        Assert.True(boolVal.Value); // default from CreateBlankMetadata

        boolVal.Value = false;
        Assert.False(((BoolValue)prop.Value).Value);
    }

    [Fact]
    public void ModifyingMetadata_ChangesPropertyInPlace()
    {
        var slot = SaveSlotFactory.CreateBlank("test.bundle");

        var prop = slot.Metadata!.AllProperties
            .First(p => p.Value is IntValue);

        var original = ((IntValue)prop.Value).Value;
        ((IntValue)prop.Value).Value = original + 100;
        Assert.Equal(original + 100, ((IntValue)prop.Value).Value);
    }
}
