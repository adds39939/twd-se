using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.PropertySets;

public class MetadataEditTests
{
    private static PropertySet CreateMetadata()
        => SaveSlotFactory.CreateBlankMetadata("WalkingDead101", "test.bundle");

    [Fact]
    public void PlaytimeProperty_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x7C725227A47FD1BA);

        Assert.NotNull(prop);
        Assert.IsType<IntValue>(prop.Value);

        var intVal = (IntValue)prop.Value;
        var original = intVal.Value;
        Assert.Equal(1, original);

        intVal.Value = 12345;
        Assert.Equal(12345, ((IntValue)prop.Value).Value);
    }

    [Fact]
    public void EpisodeProgress_CanBeReadAndModified()
    {
        var metadata = CreateMetadata();

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

        var prop = metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x4F8338150CC8BCD6);

        Assert.NotNull(prop);
        Assert.IsType<BoolValue>(prop.Value);

        var boolVal = (BoolValue)prop.Value;
        Assert.True(boolVal.Value);

        boolVal.Value = false;
        Assert.False(((BoolValue)prop.Value).Value);
    }

    private static PropertySet CreateMinimalMetadata()
    {
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

        Assert.Null(metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol));

        var int32Symbol = Symbol.FromString("int32");
        var group = metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == int32Symbol)!;
        group.Properties.Add(new Property(symbol, new IntValue(3)));

        var prop = metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == symbol);
        Assert.NotNull(prop);
        Assert.Equal(3, ((IntValue)prop.Value).Value);
    }

    [Fact]
    public void MinimalMetadata_SetBool_CreatesGroupAndPropertyWhenMissing()
    {
        var metadata = CreateMinimalMetadata();
        var gcSymbol = Symbol.FromString("mGameComplete");

        var boolSymbol = Symbol.FromString("bool");
        Assert.Null(metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == boolSymbol));

        var group = new TypeGroup(boolSymbol);
        metadata.TypeGroups.Add(group);
        group.Properties.Add(new Property(gcSymbol, new BoolValue(true)));

        var prop = metadata.AllProperties.FirstOrDefault(p => p.KeySymbol == gcSymbol);
        Assert.NotNull(prop);
        Assert.True(((BoolValue)prop.Value).Value);
    }

    [Fact]
    public void MinimalMetadata_CreatedProperties_SurviveRoundTrip()
    {
        var metadata = CreateMinimalMetadata();

        var episodeSymbol = Symbol.FromString("mEpisodeNumber");
        var int32Symbol = Symbol.FromString("int32");
        var intGroup = metadata.TypeGroups.First(g => g.TypeSymbol == int32Symbol);
        intGroup.Properties.Add(new Property(episodeSymbol, new IntValue(3)));

        var gcSymbol = Symbol.FromString("mGameComplete");
        var boolSymbol = Symbol.FromString("bool");
        var boolGroup = new TypeGroup(boolSymbol);
        metadata.TypeGroups.Add(boolGroup);
        boolGroup.Properties.Add(new Property(gcSymbol, new BoolValue(true)));

        var psWriter = new PropertySetWriter();
        var psReader = new PropertySetReader();
        var bytes = psWriter.Write(metadata);
        var reparsed = psReader.Read(bytes);

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
