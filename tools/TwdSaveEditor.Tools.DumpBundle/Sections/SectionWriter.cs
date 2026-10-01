using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.DumpBundle.Sections;

public static class SectionWriter
{
    public static void Write(string path, string outputDirectory)
    {
        var sections = MetaStreamParser.Parse(File.ReadAllBytes(path))
            ?? throw new InvalidDataException($"{path} is not a MetaStream");

        Directory.CreateDirectory(outputDirectory);
        var name = Path.GetFileName(path);
        File.WriteAllBytes(Path.Combine(outputDirectory, name + ".default"), sections.Default);
        File.WriteAllBytes(Path.Combine(outputDirectory, name + ".debug"), sections.Debug);
        File.WriteAllBytes(Path.Combine(outputDirectory, name + ".async"), sections.Async);
    }
}
