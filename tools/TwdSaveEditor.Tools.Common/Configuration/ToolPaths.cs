namespace TwdSaveEditor.Tools.Common.Configuration;

public static class ToolPaths
{
    public const string ArchivesVariable = "TWD_ARCHIVES";
    public const string SavesVariable = "TWD_SAVES";
    public const string SampleSavesVariable = "TWD_SAMPLE_SAVES";
    public const string BackupVariable = "TWD_BACKUP";

    private const string SolutionFile = "TwdSaveEditor.slnx";

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string ToolsDirectory => Path.Combine(RepositoryRoot, "tools");

    public static string TestData => Path.Combine(RepositoryRoot, "tests", "TestData");

    public static string KeyFile => Path.Combine(ToolsDirectory, "key.txt");

    public static string Archives(string[] args, int index = 0) =>
        Argument(args, index, ArchivesVariable) ?? "Archives";

    public static string? Argument(string[] args, int index, string variable)
    {
        if (args.Length > index)
        {
            return args[index];
        }

        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
