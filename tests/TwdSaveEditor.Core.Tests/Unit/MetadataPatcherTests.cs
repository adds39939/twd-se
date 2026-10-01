using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.S1.Handlers;

namespace TwdSaveEditor.Core.Tests.Unit;

public class MetadataPatcherTests
{
    [Fact]
    public void PatchMetadata_SameSize_PreservesBundle()
    {
        var slot = SaveSlotFactory.CreateBlank("_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);

        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        var epProp = parsed.Metadata!.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(epProp);
        Assert.IsType<StringValue>(epProp.Value);

        var originalValue = ((StringValue)epProp.Value).Value;
        ((StringValue)epProp.Value).Value = "WalkingDead102";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);

        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");
        var reparsedProp = reparsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.Equal("WalkingDead102", ((StringValue)reparsedProp.Value).Value);
    }

    [Fact]
    public void PatchMetadata_DifferentSize_RebuildsCorrectly()
    {
        var slot = SaveSlotFactory.CreateBlank("_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);
        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        var epProp = parsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        ((StringValue)epProp.Value).Value = "WalkingDead505_ExtraLongEpisodeNameForTesting";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);

        Assert.True(patched.Length > originalBytes.Length);

        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");
        var reparsedProp = reparsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.Equal("WalkingDead505_ExtraLongEpisodeNameForTesting", ((StringValue)reparsedProp.Value).Value);
    }

    [Fact]
    public void PatchMetadata_PreservesOtherFiles()
    {
        var registry = new SeasonRegistry([
            new S1Handler()
        ]);
        var slot = registry.CreateSave("s1", 1, "_test_autosave.bundle");
        var originalBytes = BundleWriter.Write(slot);
        var parsed = BundleReader.Read(originalBytes, "_test_autosave.bundle");

        var epProp = parsed.Metadata!.AllProperties
            .First(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        ((StringValue)epProp.Value).Value = "WalkingDead103";

        var patched = MetadataPatcher.PatchMetadata(originalBytes, parsed.Metadata, parsed.RawMetadataFile!);
        var reparsed = BundleReader.Read(patched, "_test_autosave.bundle");

        Assert.NotNull(reparsed.Choices);
        Assert.True(reparsed.Choices.AllProperties.Any());
    }
}
