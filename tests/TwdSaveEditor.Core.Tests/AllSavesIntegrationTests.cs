using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Core.Tests;

public class AllSavesIntegrationTests
{
    private static readonly string SaveDir = @"C:\Users\Adam\Downloads\twd-saves";

    private static IEnumerable<string> AllBundleFiles()
    {
        if (!Directory.Exists(SaveDir))
            return [];
        return Directory.GetFiles(SaveDir, "*.bundle", SearchOption.AllDirectories);
    }

    [Fact]
    public void AllBundleFiles_ParseWithoutCrash()
    {
        var files = AllBundleFiles().ToList();
        if (files.Count == 0) return;

        var crashes = new List<string>();
        int withChoices = 0;
        int withoutChoices = 0;

        foreach (var file in files)
        {
            try
            {
                var slot = BundleReader.Read(file);
                if (slot.Choices != null)
                    withChoices++;
                else
                    withoutChoices++;
            }
            catch (Exception ex)
            {
                crashes.Add($"{Relative(file)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(crashes.Count == 0,
            $"Parsed {withChoices + withoutChoices}/{files.Count} files ({withChoices} with choices, {withoutChoices} without). " +
            $"{crashes.Count} crash(es):\n" +
            string.Join("\n", crashes.Take(20)));
    }

    [Fact]
    public void S2SlotFiles_HaveChoicesFromSeason1Prop()
    {
        var s2Dir = Path.Combine(SaveDir, "S2");
        if (!Directory.Exists(s2Dir)) return;

        var slotFiles = Directory.GetFiles(s2Dir, "wd*.bundle", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();
        if (slotFiles.Count == 0) return;

        foreach (var file in slotFiles)
        {
            var slot = BundleReader.Read(file);
            Assert.NotNull(slot.Choices);
            Assert.Equal("season1.prop", slot.ChoicesFileName);

            var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
            var choices = accessor.GetAllChoices();
            Assert.NotEmpty(choices);
        }
    }

    [Fact(Skip = "Diagnostic dump — run manually")]
    public void DumpAllUniqueChoiceKeys()
    {
        var files = AllBundleFiles().ToList();
        if (files.Count == 0) return;

        var choicesByDir = new Dictionary<string, HashSet<string>>();
        var allKeyValues = new Dictionary<string, HashSet<string>>();

        foreach (var file in files)
        {
            try
            {
                var slot = BundleReader.Read(file);
                if (slot.Choices == null) continue;

                var accessor = new SaveAccessor(slot.Choices, slot.Metadata);
                var choices = accessor.GetAllChoices();
                var dir = GetSeasonDir(file);

                if (!choicesByDir.ContainsKey(dir))
                    choicesByDir[dir] = [];

                foreach (var (key, value) in choices)
                {
                    choicesByDir[dir].Add($"{key} = {value}");
                    if (!allKeyValues.ContainsKey(key))
                        allKeyValues[key] = [];
                    allKeyValues[key].Add(value);
                }
            }
            catch { }
        }

        var lines = new List<string>();
        lines.Add($"=== {allKeyValues.Count} unique choice keys across {files.Count} files ===");

        foreach (var dir in choicesByDir.Keys.OrderBy(k => k))
        {
            lines.Add($"\n--- {dir} ({choicesByDir[dir].Count} entries) ---");
            foreach (var entry in choicesByDir[dir].OrderBy(e => e))
                lines.Add($"  {entry}");
        }

        lines.Add("\n=== All keys with all observed values ===");
        foreach (var (key, values) in allKeyValues.OrderBy(kv => kv.Key))
            lines.Add($"  {key}: [{string.Join(", ", values.OrderBy(v => v))}]");

        Assert.Fail(string.Join("\n", lines));
    }

    private static string Relative(string path) =>
        path.Replace(SaveDir + "\\", "").Replace(SaveDir + "/", "");

    private static string GetSeasonDir(string path)
    {
        var rel = Relative(path);
        var parts = rel.Split(new[] { '\\', '/' });
        return parts.Length > 1 ? parts[0] : "root";
    }
}
