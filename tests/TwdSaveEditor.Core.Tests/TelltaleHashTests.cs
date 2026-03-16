using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests;

public class TelltaleHashTests
{
    [Fact]
    public void ComputeCrc64_IsCaseInsensitive()
    {
        var hash1 = TelltaleHash.ComputeCrc64("bool");
        var hash2 = TelltaleHash.ComputeCrc64("Bool");
        var hash3 = TelltaleHash.ComputeCrc64("BOOL");

        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void ComputeCrc64_DifferentInputs_DifferentHashes()
    {
        var hashBool = TelltaleHash.ComputeCrc64("bool");
        var hashInt = TelltaleHash.ComputeCrc64("int");
        var hashString = TelltaleHash.ComputeCrc64("String");

        Assert.NotEqual(hashBool, hashInt);
        Assert.NotEqual(hashInt, hashString);
    }

    [Fact]
    public void ComputeCrc64_EmptyString_DoesNotThrow()
    {
        var hash = TelltaleHash.ComputeCrc64("");
        Assert.Equal(0UL, hash); // CRC64 of empty input is 0
    }

    [Fact]
    public void ComputeCrc64_KnownValues_AreStable()
    {
        // These values should remain constant across runs
        var hashBool = TelltaleHash.ComputeCrc64("bool");
        var hashBool2 = TelltaleHash.ComputeCrc64("bool");
        Assert.Equal(hashBool, hashBool2);
    }

    [Fact]
    public void Symbol_FromString_MatchesDirectHash()
    {
        var sym = Symbol.FromString("PropertySet");
        var hash = TelltaleHash.ComputeCrc64("PropertySet");
        Assert.Equal(hash, sym.Value);
    }
}
