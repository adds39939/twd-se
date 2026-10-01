using TwdSaveEditor.Tools.VerifyRoundTrip.Verification;

if (args.Length == 0)
{
    Console.WriteLine("Usage: VerifyRoundTrip [--all-properties] <bundle file or directory>...");
    return 1;
}

var parseAllProperties = args.Contains("--all-properties");
var paths = args.Where(arg => arg != "--all-properties")
    .SelectMany(arg => Directory.Exists(arg) ? Directory.EnumerateFiles(arg, "*.bundle", SearchOption.AllDirectories) : [arg])
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToList();

var failures = 0;
long files = 0;
long parsed = 0;
foreach (var path in paths)
{
    var result = RoundTripVerifier.Verify(path, parseAllProperties);
    files += result.FileCount;
    parsed += result.ParsedPropertyFiles;
    if (result.Problems.Count == 0)
        continue;

    failures++;
    Console.WriteLine($"FAIL {path}");
    foreach (var problem in result.Problems)
        Console.WriteLine($"     {problem}");
}

Console.WriteLine($"{paths.Count - failures} of {paths.Count} bundles round-trip exactly ({files} inner files, {parsed} parsed as property sets).");
return failures == 0 ? 0 : 1;
