namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record EpisodeFiles(string Scripts, string Extracted)
{
    public static EpisodeFiles For(string dataDirectory, int episode)
    {
        var archive = $"WDC_pc_WalkingDead{episode}_data";
        return new EpisodeFiles(Path.Combine(dataDirectory, "lua", archive), Path.Combine(dataDirectory, "extracted", archive));
    }
}
