namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Database of all known player choices across TWD: The Telltale Definitive Series.
/// Choice keys are the actual string keys found in choices.prop save data.
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

    // ── Season 1 ──────────────────────────────────────────────────────

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
                [Opt("Spanner from inventory", "inventory_-_spanner"),
                 Opt("Other weapon", "other")]),

            // Episode 4: Around Every Corner
            Choice("s1", 4, "Killed the zombie boy in the attic",
                "killed_zombie_boy",
                [Opt("Killed him", "true"), Opt("Left him", "false")]),

            Choice("s1", 4, "How many zombies Clementine killed in prologue",
                "prologue_clementine_zombies_killed",
                [Opt("7 zombies", "7"), Opt("6 zombies", "6"), Opt("5 zombies", "5"),
                 Opt("4 zombies", "4"), Opt("3 zombies", "3")]),

            Choice("s1", 4, "Who saved Kenny in the prologue",
                "prologue_saving_kenny",
                [Opt("Lee saved Kenny", "lee"), Opt("Other", "other")]),
        ];
    }

    // ── 400 Days ──────────────────────────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason1_400Days()
    {
        return
        [
            Choice("s1_400days", 1, "Vince: Shot Danny or Justin",
                "400d_vince_shot",
                [Opt("Shot Danny", "danny"), Opt("Shot Justin", "justin")],
                "Life/Death"),

            Choice("s1_400days", 1, "Wyatt: Drove off or went back for Eddie",
                "400d_wyatt_choice",
                [Opt("Drove off", "drove"), Opt("Went back", "stayed")]),

            Choice("s1_400days", 1, "Russell: Went with Nate or stayed",
                "400d_russell_choice",
                [Opt("Went with Nate", "true"), Opt("Hid from Nate", "false")]),

            Choice("s1_400days", 1, "Bonnie: Lied to Leland",
                "400d_bonnie_lied",
                [Opt("Lied", "true"), Opt("Told the truth", "false")]),

            Choice("s1_400days", 1, "Shel: Drove away or stayed",
                "400d_shel_choice",
                [Opt("Drove away", "true"), Opt("Stayed", "false")]),
        ];
    }

    // ── Season 2 ──────────────────────────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason2()
    {
        return
        [
            Choice("s2", 1, "Saved Christa or ran",
                "s2_christa_choice",
                [Opt("Tried to save Christa", "true"), Opt("Ran away", "false")]),

            Choice("s2", 2, "Sat with Kenny or Luke at dinner",
                "s2_dinner_seat",
                [Opt("Sat with Kenny", "kenny"), Opt("Sat with Luke", "luke")],
                "Relationship"),

            Choice("s2", 3, "Watched Kenny kill Carver",
                "s2_watched_carver",
                [Opt("Stayed and watched", "true"), Opt("Looked away", "false")],
                "Life/Death"),

            Choice("s2", 4, "Robbed Arvo",
                "s2_robbed_arvo",
                [Opt("Robbed Arvo", "true"), Opt("Let him keep supplies", "false")]),

            Choice("s2", 4, "Tried to save Sarah",
                "s2_saved_sarah",
                [Opt("Tried to save Sarah", "true"), Opt("Left Sarah", "false")],
                "Life/Death"),

            Choice("s2", 5, "Season 2 ending choice",
                "s2_ending",
                [Opt("Stayed at Wellington", "wellington"),
                 Opt("Left with Kenny", "kenny"),
                 Opt("Went with Jane", "jane"),
                 Opt("Went alone", "alone")],
                "Life/Death"),
        ];
    }

    // ── Michonne ──────────────────────────────────────────────────────

    private static List<ChoiceDefinition> BuildMichonne()
    {
        return
        [
            Choice("michonne", 1, "Gave Zachary mercy",
                "m_zachary_mercy",
                [Opt("Gave mercy", "true"), Opt("Walked away", "false")],
                "Life/Death"),

            Choice("michonne", 2, "Let Randall live",
                "m_randall_alive",
                [Opt("Let Randall live", "true"), Opt("Killed Randall", "false")],
                "Life/Death"),

            Choice("michonne", 3, "Sacrificed self for the group",
                "m_sacrifice",
                [Opt("Offered self", "true"), Opt("Fought back", "false")]),
        ];
    }

    // ── Season 3: A New Frontier ──────────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason3()
    {
        return
        [
            Choice("s3", 2, "Killed Conrad or backed down",
                "s3_conrad_alive",
                [Opt("Killed Conrad", "false"), Opt("Conrad lives", "true")],
                "Life/Death"),

            Choice("s3", 3, "Stayed loyal to David",
                "s3_loyal_david",
                [Opt("Stayed loyal", "true"), Opt("Sided against", "false")],
                "Relationship"),

            Choice("s3", 5, "Went after Kate or David",
                "s3_finale_choice",
                [Opt("Went after Kate", "kate"), Opt("Went with David", "david")]),

            Choice("s3", 5, "Season 3 ending",
                "s3_ending",
                [Opt("Kate and David died", "both_dead"),
                 Opt("Kate survived", "kate"),
                 Opt("David survived", "david"),
                 Opt("Both survived", "both")],
                "Life/Death"),
        ];
    }

    // ── Season 4: The Final Season ────────────────────────────────────

    private static List<ChoiceDefinition> BuildSeason4()
    {
        return
        [
            Choice("s4", 1, "Saved Louis or Violet",
                "s4_saved_louis_violet",
                [Opt("Saved Louis", "louis"), Opt("Saved Violet", "violet")],
                "Life/Death"),

            Choice("s4", 1, "Romanced Louis or Violet",
                "s4_romance",
                [Opt("Romanced Louis", "louis"), Opt("Romanced Violet", "violet"),
                 Opt("Neither", "neither")],
                "Relationship"),

            Choice("s4", 2, "AJ killed Marlon",
                "s4_aj_killed_marlon",
                [Opt("AJ killed Marlon", "true"), Opt("Stopped AJ", "false")],
                "Life/Death"),

            Choice("s4", 3, "Killed Lilly",
                "s4_lilly_killed",
                [Opt("Killed Lilly", "true"), Opt("Spared Lilly", "false")],
                "Life/Death"),

            Choice("s4", 4, "Clementine's fate",
                "s4_clem_alive",
                [Opt("Clementine survives", "true"), Opt("Clementine dies", "false")],
                "Life/Death"),
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
