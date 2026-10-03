using System.Text.Json;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.Base.Tests.Story;

public class StoryEpisodeChaptersTests
{
    private const string Script = "Yard";

    private static readonly StoryEpisodeChapters Episode = new(1,
    [
        Chapter("Opening", "PreviouslyOn", startsEpisode: true),
        Chapter("Plain", Script),
        Chapter("ActTwo", Script, ("Act", "2")),
        Chapter("ActThree", Script, ("Act", "3")),
        Chapter("ActThreeDay", Script, ("Act", "3"), ("Night", "false")),
        Chapter("ActThreeNight", Script, ("Act", "3"), ("Night", "true")),
        Chapter("Cellar", Script, ("Place", "\"Cellar\"")),
        Chapter("Roof", Script, ("Place", "\"Roof\"")),
    ]);

    [Fact]
    public void Generated_PicksTheChapterWhoseNumberMatches()
    {
        var flags = new PropertySet();
        flags.SetInt("Act", 3);

        Assert.Equal("ActThree", Episode.Generated(Script, flags)?.Id);
    }

    [Fact]
    public void Generated_PicksTheChapterWhoseFlagMatches()
    {
        var flags = new PropertySet();
        flags.SetInt("Act", 3);
        flags.SetBool("Night", true);

        Assert.Equal("ActThreeNight", Episode.Generated(Script, flags)?.Id);
    }

    [Fact]
    public void Generated_PicksTheChapterWhoseTextMatches()
    {
        var flags = new PropertySet();
        flags.SetString("Place", "Roof");

        Assert.Equal("Roof", Episode.Generated(Script, flags)?.Id);
    }

    [Fact]
    public void Generated_PrefersTheChapterWithTheMostFlags()
    {
        var flags = new PropertySet();
        flags.SetInt("Act", 3);
        flags.SetBool("Night", false);

        Assert.Equal("ActThreeDay", Episode.Generated(Script, flags)?.Id);
    }

    [Fact]
    public void Generated_FallsBackToTheFirstChapterWithTheFlagKeysWhenNoValuesMatch()
    {
        var flags = new PropertySet();
        flags.SetInt("Act", 5);

        Assert.Equal("ActTwo", Episode.Generated(Script, flags)?.Id);
    }

    [Fact]
    public void Generated_IgnoresChaptersOfOtherScriptsAndTheOpening()
    {
        Assert.Equal("Plain", Episode.Generated(Script, new PropertySet())?.Id);
        Assert.Null(Episode.Generated("PreviouslyOn", null));
    }

    private static StoryChapter Chapter(string id, string script, params (string Key, string Json)[] flags) =>
        Chapter(id, script, false, flags);

    private static StoryChapter Chapter(string id, string script, bool startsEpisode, params (string Key, string Json)[] flags) =>
        new(id, id, "Act", script, id, startsEpisode, [.. flags.Select(flag => new StoryChapterFlag(flag.Key, JsonDocument.Parse(flag.Json).RootElement))], []);
}
