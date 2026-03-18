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

    /// <summary>
    /// Simulates editing metadata on a minimal save (like a real game save)
    /// where properties like mEpisodeNumber/mChapterNumber don't exist yet.
    /// The editor should create them.
    /// </summary>
    private static PropertySet CreateMinimalMetadata()
    {
        // Real game saves only have 2 ints + 1 string — no episode/chapter/gameComplete
        var int32Symbol = Symbol.FromString("int32");
        var stringSymbol = Symbol.FromString("String");

        return new PropertySet
        {
            Version = 2,
            Flags = 0x100,
            TypeGroups =
            [
                new TypeGroup(int32Symbol)
                {
                    Properties =
                    [
                        new Property(new Symbol(0x7C725227A47FD1BA), new IntValue(1)),
                        new Property(new Symbol(0x94C245DACB1ADDC3), new IntValue(1)),
                    ]
                },
                new TypeGroup(stringSymbol)
                {
                    Properties =
                    [
                        new Property(new Symbol(0xF235E9FCE9562E01), new StringValue("_wd1_saveslot1_autosave.bundle")),
                    ]
                },
            ]
        };
    }

    [Fact]
    public void MinimalMetadata_SetInt_CreatesPropertyWhenMissing()
    {
        var metadata = CreateMinimalMetadata();
        var symbol = Symbol.FromString("mEpisodeNumber");

        // Property shouldn't exist yet
        Assert.Null(metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol));

        // Simulate what ResumePointEditor.SetMetadataInt does
        var int32Symbol = Symbol.FromString("int32");
        var group = metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == int32Symbol)!;
        group.Properties.Add(new Property(symbol, new IntValue(3)));

        // Should exist now
        var prop = metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        Assert.NotNull(prop);
        Assert.Equal(3, ((IntValue)prop.Value).Value);
    }

    [Fact]
    public void MinimalMetadata_SetBool_CreatesGroupAndPropertyWhenMissing()
    {
        var metadata = CreateMinimalMetadata();
        var gcSymbol = Symbol.FromString("mGameComplete");

        // No bool group exists yet
        var boolSymbol = Symbol.FromString("bool");
        Assert.Null(metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == boolSymbol));

        // Simulate what ResumePointEditor does
        var group = new TypeGroup(boolSymbol);
        metadata.TypeGroups.Add(group);
        group.Properties.Add(new Property(gcSymbol, new BoolValue(true)));

        // Should exist now
        var prop = metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == gcSymbol);
        Assert.NotNull(prop);
        Assert.True(((BoolValue)prop.Value).Value);
    }

    [Fact]
    public void MinimalMetadata_CreatedProperties_SurviveRoundTrip()
    {
        var metadata = CreateMinimalMetadata();

        // Add episode number (missing in minimal save)
        var episodeSymbol = Symbol.FromString("mEpisodeNumber");
        var int32Symbol = Symbol.FromString("int32");
        var intGroup = metadata.TypeGroups.First(g => g.TypeSymbol == int32Symbol);
        intGroup.Properties.Add(new Property(episodeSymbol, new IntValue(3)));

        // Add game complete (missing entirely — no bool group)
        var gcSymbol = Symbol.FromString("mGameComplete");
        var boolSymbol = Symbol.FromString("bool");
        var boolGroup = new TypeGroup(boolSymbol);
        metadata.TypeGroups.Add(boolGroup);
        boolGroup.Properties.Add(new Property(gcSymbol, new BoolValue(true)));

        // Serialize and re-parse
        var psWriter = new TwdSaveEditor.Core.Binary.PropertySetWriter();
        var psReader = new TwdSaveEditor.Core.Binary.PropertySetReader();
        var bytes = psWriter.Write(metadata);
        var reparsed = psReader.Read(bytes);

        // Verify new properties survived
        var epProp = reparsed.AllProperties.FirstOrDefault(p => p.KeySymbol == episodeSymbol);
        Assert.NotNull(epProp);
        Assert.Equal(3, ((IntValue)epProp.Value).Value);

        var gcProp = reparsed.AllProperties.FirstOrDefault(p => p.KeySymbol == gcSymbol);
        Assert.NotNull(gcProp);
        Assert.True(((BoolValue)gcProp.Value).Value);
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
