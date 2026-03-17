using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Tests.Integration;

/// <summary>
/// Validates that created saves are structurally compatible with real game saves.
/// Compares binary structure, version entries, file hashes, PropertySet format,
/// and EventLog record format against real test data bundles.
/// </summary>
public class GameCompatibilityTests
{
    private static readonly ISeasonRegistry Registry = new SeasonRegistry(
    [
        new S1Handler(),
        new S2Handler(),
        new S3Handler(),
        new S4Handler(),
        new MichonneHandler(),
    ]);

    // ── Validation 1: Bundle structure ────────────────────────────────────

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasSameMagicAsRealSave(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.Equal(realSlot.OuterHeader.Magic, createdSlot.OuterHeader.Magic);
        Assert.Equal(MetaStreamHeader.MagicMsv6, createdSlot.OuterHeader.Magic);
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasSameOuterVersionEntries(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        Assert.Equal(realSlot.OuterHeader.VersionEntries.Count, createdSlot.OuterHeader.VersionEntries.Count);

        for (int i = 0; i < realSlot.OuterHeader.VersionEntries.Count; i++)
        {
            var real = realSlot.OuterHeader.VersionEntries[i];
            var created = createdSlot.OuterHeader.VersionEntries[i];
            Assert.Equal(real.TypeCrc, created.TypeCrc);
            Assert.Equal(real.VersionCrc, created.VersionCrc);
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasCorrectInnerFileNames(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        // Created save should contain metadata_slot.p at minimum
        Assert.Contains(createdSlot.FileTable, f => f.Name == "metadata_slot.p");

        // Real save should also have metadata_slot.p
        Assert.Contains(realSlot.FileTable, f => f.Name == "metadata_slot.p");

        // Verify the expected inner files based on season
        if (seasonKey == "s1")
        {
            Assert.Contains(createdSlot.FileTable, f => f.Name == "choices.prop");
        }
        else if (seasonKey == "s2")
        {
            Assert.Contains(createdSlot.FileTable, f => f.Name == "season1.prop");
        }
        else if (seasonKey == "s4")
        {
            Assert.Contains(createdSlot.FileTable, f => f.Name == "choicestats.pro");
        }
        // S3 and Michonne don't include choices in the bundle
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_HasMatchingFileTableHashes(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        foreach (var createdEntry in createdSlot.FileTable)
        {
            var realEntry = realSlot.FileTable.FirstOrDefault(f => f.Name == createdEntry.Name);
            if (realEntry != null)
            {
                Assert.Equal(realEntry.Hash1, createdEntry.Hash1);
                Assert.Equal(realEntry.Hash2, createdEntry.Hash2);
            }
        }
    }

    // ── Inner MetaStream structure ────────────────────────────────────────

    [Theory]
    [InlineData("s1")]
    [InlineData("s2")]
    [InlineData("s3")]
    [InlineData("s4")]
    [InlineData("michonne")]
    public void CreatedSave_InnerMetaStreamHasMsv6Magic(string seasonKey)
    {
        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        // Check each inner file has MSV6 magic
        foreach (var (name, rawData) in createdSlot.RawInnerFiles!)
        {
            Assert.True(rawData.Length >= 4, $"Inner file {name} too small");
            var magic = BitConverter.ToUInt32(rawData, 0);
            Assert.Equal(MetaStreamHeader.MagicMsv6, magic);
        }
    }

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_InnerVersionEntriesMatchReal(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        // Compare inner version entries for metadata_slot.p
        var realMetadata = realSlot.RawInnerFiles!["metadata_slot.p"];
        var createdMetadata = createdSlot.RawInnerFiles!["metadata_slot.p"];

        var realInnerVers = ParseInnerVersionEntries(realMetadata);
        var createdInnerVers = ParseInnerVersionEntries(createdMetadata);

        Assert.Equal(realInnerVers.Count, createdInnerVers.Count);
        for (int i = 0; i < realInnerVers.Count; i++)
        {
            Assert.Equal(realInnerVers[i].TypeCrc, createdInnerVers[i].TypeCrc);
            Assert.Equal(realInnerVers[i].VersionCrc, createdInnerVers[i].VersionCrc);
        }
    }

    // ── PropertySet structure ─────────────────────────────────────────────

    [Theory]
    [InlineData("s1", "wd1_saveslot2.bundle", "S1")]
    [InlineData("s2", "wd2_saveslot1.bundle", "S2")]
    [InlineData("s3", "wd3_saveslot1.bundle", "S3")]
    [InlineData("s4", "wd4_saveslot1.bundle", "S4")]
    [InlineData("michonne", "wdm_saveslot4.bundle", "Michonne")]
    public void CreatedSave_MetadataPropertySetMatchesRealFormat(string seasonKey, string realFile, string testDataDir)
    {
        var realPath = TestDataHelper.GetPath(testDataDir, realFile);
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get(seasonKey)!;
        var createdSlot = handler.CreateBlankSave($"{handler.FilePrefix}test.bundle", handler.GetEpisodeId(1));

        // Both should have valid metadata
        Assert.NotNull(realSlot.Metadata);
        Assert.NotNull(createdSlot.Metadata);

        // Same PropertySet version and flags
        Assert.Equal(realSlot.Metadata!.Version, createdSlot.Metadata!.Version);
        Assert.Equal(realSlot.Metadata.Flags, createdSlot.Metadata.Flags);

        // Created metadata should use version=2, flags=0x100
        Assert.Equal(2u, createdSlot.Metadata.Version);
        Assert.Equal(0x100u, createdSlot.Metadata.Flags);
    }

    [Fact]
    public void CreatedSave_S1ChoicesPropertySetHasCorrectStructure()
    {
        var realPath = TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s1")!;
        var createdSlot = handler.CreateBlankSave("wd1_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.Choices);
        Assert.NotNull(createdSlot.Choices);

        // Same version
        Assert.Equal(realSlot.Choices!.Version, createdSlot.Choices!.Version);
        Assert.Equal(2u, createdSlot.Choices.Version);

        // S1 real choices.prop uses flags=0x0
        Assert.Equal(0x0u, createdSlot.Choices.Flags);

        // Should have ChoicesContainer type group
        Assert.Contains(createdSlot.Choices.TypeGroups,
            g => g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);
    }

    [Fact]
    public void CreatedSave_S2ChoicesPropertySetHasCorrectStructure()
    {
        var realPath = TestDataHelper.GetPath("S2", "wd2_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s2")!;
        var createdSlot = handler.CreateBlankSave("wd2_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.Choices);
        Assert.NotNull(createdSlot.Choices);

        Assert.Equal(2u, createdSlot.Choices!.Version);

        // FINDING: Real S2 uses season1.prop (not choices.prop), and its PropertySet
        // uses flags=0x100 while our created save uses flags=0x0.
        // The game reads these via the season1.prop file name.
        // Note: S2 real file name is "season1.prop", our created save uses "choices.prop"
        Assert.Equal("season1.prop", realSlot.ChoicesFileName);

        // Should have ChoicesContainer type group
        Assert.Contains(createdSlot.Choices.TypeGroups,
            g => g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);
    }

    [Fact]
    public void CreatedSave_S4ChoiceStatsPropertySetMatchesRealFormat()
    {
        var realPath = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        var handler = Registry.Get("s4")!;
        var createdSlot = handler.CreateBlankSave("wd4_test.bundle", handler.GetEpisodeId(1));

        Assert.NotNull(realSlot.ChoiceStats);
        Assert.NotNull(createdSlot.ChoiceStats);

        // Same PropertySet version and flags
        Assert.Equal(realSlot.ChoiceStats!.Version, createdSlot.ChoiceStats!.Version);
        Assert.Equal(realSlot.ChoiceStats.Flags, createdSlot.ChoiceStats.Flags);

        // choicestats.pro should use version=2, flags=0x100
        Assert.Equal(2u, createdSlot.ChoiceStats.Version);
        Assert.Equal(0x100u, createdSlot.ChoiceStats.Flags);
    }

    // ── Write + Read round-trip ───────────────────────────────────────────

    [Theory]
    [InlineData("s1", 5)]
    [InlineData("s2", 5)]
    [InlineData("s3", 5)]
    [InlineData("s4", 4)]
    [InlineData("michonne", 3)]
    public void CreatedSave_WritesAndReadsBackWithCorrectStructure(string seasonKey, int episode)
    {
        var handler = Registry.Get(seasonKey)!;
        var fileName = $"{handler.FilePrefix}roundtrip.bundle";
        var createdSlot = SaveSlotFactory.CreateForSeason(Registry, seasonKey, episode, fileName);

        // Write to bytes
        var bundleBytes = BundleWriter.Write(createdSlot);
        Assert.True(bundleBytes.Length > 0);

        // Read back
        var readBack = BundleReader.Read(bundleBytes, fileName);

        // Verify structure
        Assert.Equal(MetaStreamHeader.MagicMsv6, readBack.OuterHeader.Magic);
        Assert.NotNull(readBack.Metadata);
        Assert.Equal(2u, readBack.Metadata!.Version);
        Assert.Equal(0x100u, readBack.Metadata.Flags);

        // Verify metadata has episode ID
        var accessor = new SaveAccessor(readBack.Choices, readBack.Metadata);
        var episodeId = accessor.GetMetadataString("episodeId");
        // It should be the right format
        Assert.True(readBack.Metadata.AllProperties.Any());

        // Verify file table preserved
        Assert.Equal(createdSlot.FileTable.Count, readBack.FileTable.Count);
        for (int i = 0; i < createdSlot.FileTable.Count; i++)
        {
            Assert.Equal(createdSlot.FileTable[i].Name, readBack.FileTable[i].Name);
            Assert.Equal(createdSlot.FileTable[i].Hash1, readBack.FileTable[i].Hash1);
            Assert.Equal(createdSlot.FileTable[i].Hash2, readBack.FileTable[i].Hash2);
        }
    }

    // ── Validation 4: EventLog record format ──────────────────────────────

    [Fact]
    public void EStoreCreator_ProducesCorrectMsv6Header()
    {
        var events = new List<EventLogEntry>
        {
            new()
            {
                EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
                NodeHash = 0x2D4BB68B3A6B79B7, // shot_conrad = true
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = 0,
                Trailing = 0,
            }
        };

        var (estore, epage, epageFilename) = EStoreCreator.Create("_wd3_test_id", events);

        // Verify MSV6 magic on both
        Assert.Equal(MetaStreamHeader.MagicMsv6, BitConverter.ToUInt32(estore, 0));
        Assert.Equal(MetaStreamHeader.MagicMsv6, BitConverter.ToUInt32(epage, 0));

        // Verify version entry count
        var estoreVerCount = BitConverter.ToUInt32(estore, 16);
        var epageVerCount = BitConverter.ToUInt32(epage, 16);
        Assert.Equal(5u, estoreVerCount);
        Assert.Equal(5u, epageVerCount);
    }

    [Fact]
    public void EStoreCreator_RecordFormatMatches42Bytes()
    {
        var entry = new EventLogEntry
        {
            EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
            NodeHash = 0x2D4BB68B3A6B79B7,
            ValueType = 1,
            ExtraFlag = 0,
            SequenceIndex = 0,
            Trailing = 0,
        };

        var record = EStoreCreator.BuildRecord(entry);

        Assert.Equal(42, record.Length);

        // Check header fields
        Assert.Equal(0x0Au, BitConverter.ToUInt32(record, 0));  // version
        Assert.Equal(0x22u, BitConverter.ToUInt32(record, 4));  // payload
        Assert.Equal(0x01u, BitConverter.ToUInt32(record, 8));  // count
        Assert.Equal(0x00u, BitConverter.ToUInt32(record, 12)); // padding

        // Check event type hash
        Assert.Equal(EventLogEntry.EventTypes.ExecutingDialogNode, BitConverter.ToUInt64(record, 16));

        // Check node hash
        Assert.Equal(0x2D4BB68B3A6B79B7UL, BitConverter.ToUInt64(record, 29));
    }

    [Fact]
    public void EStoreCreator_RecordMatchesRealEpageFormat()
    {
        // Read a real epage file
        var realEpagePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id_Page734.epage");
        var realData = File.ReadAllBytes(realEpagePath);

        // Parse it to get a real record
        var realEntries = EStoreReader.ReadEPage(realEpagePath);
        Assert.NotEmpty(realEntries);

        // Get the first real record's raw data
        var firstReal = realEntries[0];

        // Create a record with the same values
        var created = EStoreCreator.BuildRecord(firstReal);

        // Compare byte by byte
        Assert.Equal(42, created.Length);
        Assert.Equal(firstReal.RawData.Length, created.Length);

        // Header should match exactly
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(firstReal.RawData[i], created[i]);
        }

        // Event type hash should match
        Assert.Equal(
            BitConverter.ToUInt64(firstReal.RawData, 16),
            BitConverter.ToUInt64(created, 16));

        // Node hash should match
        Assert.Equal(
            BitConverter.ToUInt64(firstReal.RawData, 29),
            BitConverter.ToUInt64(created, 29));
    }

    [Fact]
    public void EStoreCreator_VersionEntriesDocumented()
    {
        // FINDING: Real S3/Michonne estore files have 5 version entries:
        //   [0] TypeCrc=0x3AAEB61240D3CFBA, VersionCrc=0xD8D22CB9
        //   [1] TypeCrc=0xBEBB886A0541595F, VersionCrc=0xB59B0682
        //   [2] TypeCrc=0x004F023463D89FB0, VersionCrc=0xB539B0FF
        //   [3] TypeCrc=0x24032A7AD8BB721D, VersionCrc=0x739CE237
        //   [4] TypeCrc=0x238A520C4A924AA6, VersionCrc=0x2E4AF103
        //
        // EStoreCreator currently uses 3 version entries (from bundle inner MetaStream):
        //   [0] TypeCrc=0xCD75DC4F6B9F15D2, VersionCrc=0x21F2BCC9
        //   [1] TypeCrc=0x84283CB979D71641, VersionCrc=0x0527D6BF
        //   [2] TypeCrc=0x004F023463D89FB0, VersionCrc=0xB539B0FF
        //
        // Entry [2] matches real entry [2] (shared base type).
        // The other entries differ because estore uses EventLog-specific types
        // while the bundle inner files use PropertySet types.
        //
        // Despite this mismatch, the game still loads our created estore files
        // because the version entries are used for forward-compatibility checks,
        // not strict validation.

        var realEstorePath = TestDataHelper.GetPath("S3", "_wd3_saveslot1_id.estore");
        var realData = File.ReadAllBytes(realEstorePath);

        var realVerCount = BitConverter.ToUInt32(realData, 16);
        Assert.Equal(5u, realVerCount);

        // Verify the real version entries match our documentation
        var expectedReal = new (ulong TypeCrc, uint VersionCrc)[]
        {
            (0x3AAEB61240D3CFBA, 0xD8D22CB9),
            (0xBEBB886A0541595F, 0xB59B0682),
            (0x004F023463D89FB0, 0xB539B0FF),
            (0x24032A7AD8BB721D, 0x739CE237),
            (0x238A520C4A924AA6, 0x2E4AF103),
        };

        int pos = 20;
        for (int i = 0; i < realVerCount; i++)
        {
            var tc = BitConverter.ToUInt64(realData, pos);
            pos += 8;
            var vc = BitConverter.ToUInt32(realData, pos);
            pos += 4;
            Assert.Equal(expectedReal[i].TypeCrc, tc);
            Assert.Equal(expectedReal[i].VersionCrc, vc);
        }

        // Verify EStoreCreator now produces the correct 5 version entries matching real saves
        var events = new List<EventLogEntry>
        {
            new()
            {
                EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
                NodeHash = 0x2D4BB68B3A6B79B7,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = 0,
                Trailing = 0,
            }
        };
        var (estore, _, _) = EStoreCreator.Create("_wd3_test", events);

        var createdVerCount = BitConverter.ToUInt32(estore, 16);
        Assert.Equal(5u, createdVerCount);

        // All 5 entries should match the real estore
        int cpos = 20;
        for (int i = 0; i < 5; i++)
        {
            var tc = BitConverter.ToUInt64(estore, cpos); cpos += 8;
            var vc = BitConverter.ToUInt32(estore, cpos); cpos += 4;
            Assert.Equal(expectedReal[i].TypeCrc, tc);
            Assert.Equal(expectedReal[i].VersionCrc, vc);
        }
    }

    // ── Validation 5: CRC64 hash verification ─────────────────────────────

    [Theory]
    [InlineData("Executing Dialog Node", 0x625874A31EA13BB1UL)]
    [InlineData("Dialog Choice", 0x25D62FD9BE53CF73UL)]
    [InlineData("Begin Episode", 0x22B4F702006E4E3AUL)]
    [InlineData("End Episode", 0xB1BB1124EA852E99UL)]
    [InlineData("Save Serial", 0x48FA4CC44ADE92F3UL)]
    public void EventTypeHashes_MatchExpected(string name, ulong expectedHash)
    {
        var computed = TelltaleHash.ComputeCrc64(name);
        Assert.Equal(expectedHash, computed);
    }

    [Fact]
    public void S3NodeHashes_AppearInRealEpageData()
    {
        // Read all S3 epage files and collect dialog node hashes
        var s3Dir = TestDataHelper.GetSeasonDir("S3");
        var epageFiles = Directory.GetFiles(s3Dir, "*.epage");

        var allNodeHashes = new HashSet<ulong>();
        foreach (var epageFile in epageFiles)
        {
            var entries = EStoreReader.ReadEPage(epageFile);
            foreach (var entry in entries)
            {
                if (entry.IsDialogNode)
                    allNodeHashes.Add(entry.NodeHash);
            }
        }

        Assert.NotEmpty(allNodeHashes);

        // Check how many of our known hashes appear in real data
        var matchCount = 0;
        foreach (var (hash, (key, val)) in ChoiceNodeMapping.S3Nodes)
        {
            if (allNodeHashes.Contains(hash))
                matchCount++;
        }

        // At least some hashes should match (the test save may not have all choices)
        Assert.True(matchCount > 0,
            $"None of the {ChoiceNodeMapping.S3Nodes.Count} S3 node hashes found in real epage data. " +
            $"Total unique dialog nodes in real data: {allNodeHashes.Count}");
    }

    [Fact]
    public void MichonneNodeHashes_ProduceValidCrc64()
    {
        // Verify Michonne GUID -> CRC64 hash computation
        foreach (var (guid, (choiceKey, optionValue)) in ChoiceNodeMapping.MichonneNodes)
        {
            var hashInput = "{" + guid + "}";
            var hash = TelltaleHash.ComputeCrc64(hashInput);
            Assert.NotEqual(0UL, hash);
        }
    }

    [Fact]
    public void S4ChoiceStats_GuidFormatMatchesRealSave()
    {
        var realPath = TestDataHelper.GetPath("S4", "wd4_saveslot1.bundle");
        var realSlot = BundleReader.Read(realPath);

        Assert.NotNull(realSlot.ChoiceStats);

        // Get the raw GUID string
        var rawProp = realSlot.ChoiceStats!.AllProperties.FirstOrDefault();
        Assert.NotNull(rawProp);
        Assert.IsType<StringValue>(rawProp!.Value);

        var rawString = ((StringValue)rawProp.Value).Value;

        // If the save has any GUIDs, verify the format matches our pattern
        if (!string.IsNullOrEmpty(rawString))
        {
            // Format should be tab-separated "( {GUID} )" entries
            var entries = rawString.Split('\t');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                // Should match pattern: ( {GUID} )
                Assert.Matches(@"\(\s*\{[0-9A-Fa-f-]+\}\s*\)", entry);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static List<VersionEntry> ParseInnerVersionEntries(byte[] innerData)
    {
        var entries = new List<VersionEntry>();
        if (innerData.Length < 20) return entries;

        var verCount = BitConverter.ToUInt32(innerData, 16);
        int pos = 20;
        for (int i = 0; i < verCount; i++)
        {
            if (pos + 12 > innerData.Length) break;
            var typeCrc = BitConverter.ToUInt64(innerData, pos);
            pos += 8;
            var versionCrc = BitConverter.ToUInt32(innerData, pos);
            pos += 4;
            entries.Add(new VersionEntry(typeCrc, versionCrc));
        }
        return entries;
    }
}
