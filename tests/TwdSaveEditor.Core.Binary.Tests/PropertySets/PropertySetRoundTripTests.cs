using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.PropertySets;

public class PropertySetRoundTripTests
{
    private readonly PropertySetReader _reader = new();
    private readonly PropertySetWriter _writer = new();

    [Fact]
    public void EmptyPropertySet_RoundTrips()
    {
        var original = new PropertySet();
        var bytes = _writer.Write(original);
        var result = _reader.Read(bytes);

        Assert.Empty(result.ParentSymbols);
        Assert.Empty(result.TypeGroups);
    }

    [Fact]
    public void PropertySet_WithStringArrays_RoundTrips()
    {
        var original = new PropertySet();
        original.SetStrings("Items - Clementine", ["ui_item_hammer", "ui_item_watch"]);
        original.SetStrings("Group - Hidden Children", []);
        original.SetBool("Runtime: Visible", false);

        var bytes = _writer.Write(original);
        var result = _reader.Read(bytes);

        Assert.Equal(["ui_item_hammer", "ui_item_watch"], result.GetStrings("Items - Clementine"));
        Assert.Empty(result.GetStrings("Group - Hidden Children")!);
        Assert.False(result.GetBool("Runtime: Visible"));
        Assert.Equal(bytes, _writer.Write(result));
    }

    [Fact]
    public void PropertySet_WithBoolProperties_RoundTrips()
    {
        var boolTypeSymbol = Symbol.FromString("bool");
        var original = new PropertySet
        {
            TypeGroups =
            [
                new TypeGroup(boolTypeSymbol)
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mbAlive"), new BoolValue(true)),
                        new Property(Symbol.FromString("mbDead"), new BoolValue(false))
                    ]
                }
            ]
        };

        var bytes = _writer.Write(original);
        var result = _reader.Read(bytes);

        Assert.Single(result.TypeGroups);
        var group = result.TypeGroups[0];
        Assert.Equal(boolTypeSymbol, group.TypeSymbol);
        Assert.Equal(2, group.Properties.Count);

        var prop0 = (BoolValue)group.Properties[0].Value;
        var prop1 = (BoolValue)group.Properties[1].Value;
        Assert.True(prop0.Value);
        Assert.False(prop1.Value);
    }

    [Fact]
    public void PropertySet_WithMixedTypes_RoundTrips()
    {
        var original = new PropertySet
        {
            ParentSymbols = [new Symbol(0x1234)],
            TypeGroups =
            [
                new TypeGroup(Symbol.FromString("int32"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mHealth"), new IntValue(100)),
                        new Property(Symbol.FromString("mStamina"), new IntValue(75))
                    ]
                },
                new TypeGroup(Symbol.FromString("float"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mMorale"), new FloatValue(0.8f))
                    ]
                },
                new TypeGroup(Symbol.FromString("String"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mSaveName"), new StringValue("My Save")),
                        new Property(Symbol.FromString("mSceneName"), new StringValue(""))
                    ]
                },
                new TypeGroup(Symbol.FromString("Symbol"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mChoiceId"), new SymbolValue(new Symbol(0xABCD)))
                    ]
                },
                new TypeGroup(Symbol.FromString("bool"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mGameComplete"), new BoolValue(false))
                    ]
                }
            ]
        };

        var bytes = _writer.Write(original);
        var result = _reader.Read(bytes);

        Assert.Single(result.ParentSymbols);
        Assert.Equal(0x1234UL, result.ParentSymbols[0].Value);

        Assert.Equal(5, result.TypeGroups.Count);

        var intGroup = result.TypeGroups[0];
        Assert.Equal(Symbol.FromString("int32"), intGroup.TypeSymbol);
        Assert.Equal(100, ((IntValue)intGroup.Properties[0].Value).Value);
        Assert.Equal(75, ((IntValue)intGroup.Properties[1].Value).Value);

        var floatGroup = result.TypeGroups[1];
        Assert.Equal(0.8f, ((FloatValue)floatGroup.Properties[0].Value).Value);

        var stringGroup = result.TypeGroups[2];
        Assert.Equal("My Save", ((StringValue)stringGroup.Properties[0].Value).Value);
        Assert.Equal("", ((StringValue)stringGroup.Properties[1].Value).Value);

        var symGroup = result.TypeGroups[3];
        Assert.Equal(0xABCDUL, ((SymbolValue)symGroup.Properties[0].Value).Value.Value);

        var boolGroup = result.TypeGroups[4];
        Assert.False(((BoolValue)boolGroup.Properties[0].Value).Value);
    }

    [Fact]
    public void PropertySet_NestedPropertySet_RoundTrips()
    {
        var inner = new PropertySet
        {
            TypeGroups =
            [
                new TypeGroup(Symbol.FromString("int32"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mValue"), new IntValue(42))
                    ]
                }
            ]
        };

        var original = new PropertySet
        {
            TypeGroups =
            [
                new TypeGroup(Symbol.FromString("PropertySet"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mInventory"), new PropertySetValue(inner))
                    ]
                }
            ]
        };

        var bytes = _writer.Write(original);
        var result = _reader.Read(bytes);

        Assert.Single(result.TypeGroups);
        var nested = (PropertySetValue)result.TypeGroups[0].Properties[0].Value;
        Assert.Single(nested.Value.TypeGroups);
        Assert.Equal(42, ((IntValue)nested.Value.TypeGroups[0].Properties[0].Value).Value);
    }

    [Fact]
    public void PropertySet_BinaryRoundTrip_IsIdentical()
    {
        var original = new PropertySet
        {
            TypeGroups =
            [
                new TypeGroup(Symbol.FromString("bool"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mbAlive"), new BoolValue(true))
                    ]
                },
                new TypeGroup(Symbol.FromString("int32"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mHealth"), new IntValue(100))
                    ]
                },
                new TypeGroup(Symbol.FromString("String"))
                {
                    Properties =
                    [
                        new Property(Symbol.FromString("mName"), new StringValue("Clementine"))
                    ]
                }
            ]
        };

        var bytes1 = _writer.Write(original);
        var readBack = _reader.Read(bytes1);
        var bytes2 = _writer.Write(readBack);

        Assert.Equal(bytes1, bytes2);
    }
}
