namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Database of all known player choices across TWD: The Telltale Definitive Series.
/// Choice keys are the actual string keys found in choices.prop save data.
/// Keys verified from real Definitive Edition save files are marked [VERIFIED].
/// </summary>
public static class ChoiceDatabase
{
    public static IReadOnlyList<ChoiceDefinition> AllChoices { get; } = BuildAll();

    public static IEnumerable<ChoiceDefinition> ForSeason(string seasonKey)
        => AllChoices.Where(c => c.SeasonKey.Equals(seasonKey, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ChoiceDefinition> ForEpisode(string seasonKey, int episode)
        => ForSeason(seasonKey).Where(c => c.Episode == episode);

    private static List<ChoiceDefinition> BuildAll()
    {
        var choices = new List<ChoiceDefinition>();
        choices.AddRange(BuildSeason1());
        choices.AddRange(BuildSeason1_400Days());
        choices.AddRange(BuildSeason2());
        choices.AddRange(BuildMichonne());
        choices.AddRange(BuildSeason3());
        choices.AddRange(BuildSeason4());
        return choices;
    }

    // ── Season 1 (ALL KEYS VERIFIED from real saves) ─────────────────

    private static List<ChoiceDefinition> BuildSeason1()
    {
        return
        [
            // Episode 1: A New Day
            Choice("s1", 1, "Lied to Hershel about your past",
                "lied_to_hershel",
                [Opt("Told the truth", "false"), Opt("Lied", "true")]),

            Choice("s1", 1, "Who did you save at Hershel's farm",
                "shawnduck_choice",
                [Opt("Saved Shawn", "shawn"), Opt("Saved Duck", "duck")]),

            Choice("s1", 1, "Sided with Kenny at the drug store",
                "sided_with_kenny",
                [Opt("Sided with Kenny", "true"), Opt("Sided with Larry", "false")],
                "Relationship"),

            Choice("s1", 1, "Gave Irene the gun",
                "gave_irene_gun",
                [Opt("Gave her the gun", "true"), Opt("Refused", "false")]),

            Choice("s1", 1, "Saved Doug or Carley at the drug store",
                "dougcarley_saved",
                [Opt("Saved Doug", "doug"), Opt("Saved Carley", "carley")],
                "Life/Death"),

            // Episode 2: Starved for Help
            Choice("s1", 2, "Helped kill Larry or tried to save him",
                "helped_kill_larry",
                [Opt("Helped Kenny kill Larry", "true"), Opt("Tried to save Larry", "false")],
                "Life/Death"),

            Choice("s1", 2, "Shot Jolene in the woods",
                "shot_jolene",
                [Opt("Shot Jolene", "true"), Opt("Didn't shoot", "false")]),

            Choice("s1", 2, "Chopped off the teacher's leg",
                "chopped_leg",
                [Opt("Chopped off the leg", "true"), Opt("Left him", "false")]),

            // Episode 3: Long Road Ahead
            Choice("s1", 3, "Who shot Duck",
                "kill_duck_choice",
                [Opt("Lee shot Duck", "lee"), Opt("Kenny shot Duck", "kenny"),
                 Opt("Duck wasn't shot", "neither")],
                "Life/Death"),

            Choice("s1", 3, "Left Lilly behind",
                "left_lilly",
                [Opt("Left Lilly on the road", "true"), Opt("Let her in the RV", "false")],
                "Life/Death"),

            Choice("s1", 3, "Fought with Kenny",
                "fought_kenny",
                [Opt("Fought Kenny", "true"), Opt("Didn't fight", "false")],
                "Relationship"),

            Choice("s1", 3, "Got punched by Kenny",
                "got_punched",
                [Opt("Got punched", "true"), Opt("Avoided it", "false")]),

            Choice("s1", 3, "Shot Beatrice",
                "shot_beatrice",
                [Opt("Shot Beatrice", "true"), Opt("Didn't shoot", "false")]),

            Choice("s1", 3, "Who did you help first at the overpass",
                "christaomid_choice",
                [Opt("Helped Omid first", "omid"), Opt("Helped Christa first", "christa")]),

            Choice("s1", 3, "Weapon choice",
                "weapon_choice",
                [Opt("Spike remover", "inventory_-_spike_remover"),
                 Opt("Spanner", "inventory_-_spanner"),
                 Opt("Other weapon", "other")]),

            Choice("s1", 3, "Lost temper",
                "lost_temper",
                [Opt("Lost temper", "true"), Opt("Kept calm", "false")]),

            Choice("s1", 3, "Saved Ben",
                "saved_ben",
                [Opt("Saved Ben", "true"), Opt("Let Ben fall", "false")],
                "Life/Death"),

            // Episode 4: Around Every Corner
            Choice("s1", 4, "Killed the zombie boy in the attic",
                "killed_zombie_boy",
                [Opt("Killed him", "true"), Opt("Left him", "false")]),

            Choice("s1", 4, "Brought Clementine to Crawford",
                "brought_clementine_to_crawford",
                [Opt("Brought her", "true"), Opt("Left her behind", "false")]),

            Choice("s1", 4, "Killed Stephanie (Crawford)",
                "killed_stephanie",
                [Opt("Killed Stephanie", "true"), Opt("Didn't kill", "false")]),

            Choice("s1", 4, "Found Molly videotape",
                "found_molly_videotape",
                [Opt("Found it", "true"), Opt("Missed it", "false")]),

            Choice("s1", 4, "Watched Molly videotape",
                "watched_molly_videotape",
                [Opt("Watched it", "true"), Opt("Didn't watch", "false")]),

            Choice("s1", 4, "Found Crawford pamphlet",
                "found_crawford_pamphlet",
                [Opt("Found it", "true"), Opt("Missed it", "false")]),

            Choice("s1", 4, "Found Clementine's drawings",
                "found_clementine_drawings",
                [Opt("Found them", "true"), Opt("Missed them", "false")]),

            Choice("s1", 4, "Molly's fate",
                "molly_fate",
                [Opt("Clem shoots zombie", "clem_shoots_zombie"),
                 Opt("Molly left", "molly_left"),
                 Opt("Molly stayed", "molly_stayed")]),

            Choice("s1", 4, "Ben asks for advice",
                "ben_asks_for_advice",
                [Opt("Helped Ben", "true"), Opt("Refused", "false")]),

            Choice("s1", 4, "Surrendered the cleaver",
                "surrendered_cleaver",
                [Opt("Surrendered", "true"), Opt("Kept it", "false")]),

            Choice("s1", 4, "Threatened or lied to Vernon",
                "threatened_or_lied_to_vernon",
                [Opt("Threatened/lied", "true"), Opt("Honest", "false")]),

            Choice("s1", 4, "Sewer slippery slide direction",
                "sewer_slippery_slide",
                [Opt("Center", "center"), Opt("Left", "left"), Opt("Right", "right")]),

            Choice("s1", 4, "How many zombies Clementine killed in prologue",
                "prologue_clementine_zombies_killed",
                [Opt("7 zombies", "7"), Opt("6 zombies", "6"), Opt("5 zombies", "5"),
                 Opt("4 zombies", "4"), Opt("3 zombies", "3")]),

            Choice("s1", 4, "Who saved Kenny in the prologue",
                "prologue_saving_kenny",
                [Opt("Lee saved Kenny", "lee"), Opt("Other", "other")]),

            Choice("s1", 4, "Lee blocks Molly first punch",
                "lee_blocks_molly_first_punch",
                [Opt("Blocked", "true"), Opt("Hit", "false")]),

            Choice("s1", 4, "Lee blocks Molly second punch",
                "lee_blocks_molly_second_punch",
                [Opt("Blocked", "true"), Opt("Hit", "false")]),

            // Episode 5: No Time Left
            Choice("s1", 5, "Cut off Lee's arm",
                "cut_off_arm",
                [Opt("Cut off arm", "true"), Opt("Left arm", "false")],
                "Life/Death"),

            Choice("s1", 5, "Hid the bite",
                "hid_bite",
                [Opt("Hid it", "true"), Opt("Showed it", "false")]),

            Choice("s1", 5, "Revealed the bite",
                "reveal_bite",
                [Opt("First opportunity", "first_opportunity"),
                 Opt("Later", "later"), Opt("Never", "never")]),

            Choice("s1", 5, "Party composition",
                "party_lee_ben_kenny_christa_omid",
                [Opt("Full party", "true"), Opt("Partial party", "false")]),

            Choice("s1", 5, "With Kenny at the end",
                "with_kenny",
                [Opt("With Kenny", "true"), Opt("Without Kenny", "false")]),

            Choice("s1", 5, "With Ben at the end",
                "with_ben",
                [Opt("With Ben", "true"), Opt("Without Ben", "false")]),

            Choice("s1", 5, "With Christa and Omid at the end",
                "with_christa_and_omid",
                [Opt("With them", "true"), Opt("Without them", "false")]),

            Choice("s1", 5, "Lee grabbed Kenny's hand",
                "lee_grabbed_kennys_hand",
                [Opt("Grabbed", "true"), Opt("Didn't grab", "false")]),

            Choice("s1", 5, "Killed the campman (Stranger)",
                "killed_campman",
                [Opt("Killed him", "true"), Opt("Left him", "false")],
                "Life/Death"),

            Choice("s1", 5, "Looked in campman's bag",
                "looked_in_campman_bag",
                [Opt("Looked", "true"), Opt("Didn't look", "false")]),

            Choice("s1", 5, "Final zombie interaction",
                "final_zombie_interaction",
                [Opt("Grabbed radio", "grab_radio"),
                 Opt("Other action", "other")]),

            Choice("s1", 5, "Lee's cliffhanger line",
                "lee_cliffhanger_line",
                [Opt("You are dead", "youaredead"),
                 Opt("Find Omid and Christa", "findomidandchrista"),
                 Opt("Don't be afraid", "dontbeafraid"),
                 Opt("Keep your hair short", "keepyourhairshort")]),

            Choice("s1", 5, "Clementine shot Lee",
                "clementine_shot_lee",
                [Opt("Shot Lee", "true"), Opt("Left Lee", "false")],
                "Life/Death"),

            Choice("s1", 5, "Saw the belltower stairs tutorial",
                "player_saw_belltower_stairs_tutorial",
                [Opt("Saw it", "true"), Opt("Missed it", "false")]),
        ];
    }

    // ── 400 Days (ALL KEYS VERIFIED from real saves) ─────────────────

    private static List<ChoiceDefinition> BuildSeason1_400Days()
    {
        return
        [
            Choice("s1_400days", 1, "Vince: Shot Danny or Justin",
                "shot_dan",
                [Opt("Shot Danny", "true"), Opt("Shot Justin", "false")],
                "Life/Death"),

            Choice("s1_400days", 1, "Wyatt: Left Eddie or stayed",
                "left_eddie",
                [Opt("Left Eddie", "true"), Opt("Stayed with Eddie", "false")]),

            Choice("s1_400days", 1, "Russell: Left Nate or stayed",
                "left_nate",
                [Opt("Left Nate", "true"), Opt("Stayed with Nate", "false")]),

            Choice("s1_400days", 1, "Bonnie: Lied to Leland",
                "lied_to_leland",
                [Opt("Lied", "true"), Opt("Told the truth", "false")]),

            Choice("s1_400days", 1, "Russell went with Tavia",
                "russell_went",
                [Opt("Went", "true"), Opt("Stayed", "false")]),

            Choice("s1_400days", 1, "Wyatt went with Tavia",
                "wyatt_went",
                [Opt("Went", "true"), Opt("Stayed", "false")]),

            Choice("s1_400days", 1, "Shel went with Tavia",
                "shel_went",
                [Opt("Went", "true"), Opt("Stayed", "false")]),

            Choice("s1_400days", 1, "Vince went with Tavia",
                "vince_went",
                [Opt("Went", "true"), Opt("Stayed", "false")]),
        ];
    }

    // ── Season 2 (keys NOT verified — no slot saves available) ───────

    private static List<ChoiceDefinition> BuildSeason2()
    {
        return
        [
            // Placeholder entries — keys are guesses based on S1 naming patterns.
            // Need real S2 slot saves (wd2_saveslot*.bundle with choices.prop) to verify.
        ];
    }

    // ── Michonne (keys NOT verified — only checkpoint saves available) ─

    private static List<ChoiceDefinition> BuildMichonne()
    {
        return
        [
            // Placeholder — need main slot saves (wdm_saveslot*.bundle with choices.prop)
        ];
    }

    // ── Season 3 (keys NOT verified) ─────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason3()
    {
        return
        [
            // Placeholder — need real S3 slot saves to verify
        ];
    }

    // ── Season 4 (keys NOT verified) ─────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason4()
    {
        return
        [
            // Placeholder — need real S4 slot saves to verify
        ];
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static ChoiceDefinition Choice(
        string season, int episode, string desc,
        string choiceKey, ChoiceOption[] options,
        string category = "Major")
    {
        return new ChoiceDefinition
        {
            SeasonKey = season,
            Episode = episode,
            Description = desc,
            ChoiceKey = choiceKey,
            Options = options,
            Category = category,
        };
    }

    private static ChoiceOption Opt(string label, string value)
        => new() { Label = label, Value = value };
}
