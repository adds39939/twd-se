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

    // ── Season 2 (descriptions from game's choice.prop) ────────────────

    private static List<ChoiceDefinition> BuildSeason2()
    {
        return
        [
            // Sourced from WDC_pc_ProjectSeason2_data.ttarch2/choice.prop
            Choice("s2", 1, "Did you try to save Christa?",
                "helped_christa",
                [Opt("Stopped to help Christa", "true"), Opt("Ran away", "false")],
                "Survival"),
            Choice("s2", 1, "Did you kill the dog?",
                "killed_dog",
                [Opt("Killed the dog", "true"), Opt("Walked away from the dog", "false")],
                "Mercy"),
            Choice("s2", 1, "Did you accept Nick's apology?",
                "accepted_nick_apology",
                [Opt("Accepted Nick's apology", "true"), Opt("Did not accept", "false")],
                "Trust"),
            Choice("s2", 1, "Did you save Nick or Pete?",
                "petenick_saved",
                [Opt("Went with Pete", "pete"), Opt("Went with Nick", "nick")],
                "Life/Death"),
            Choice("s2", 1, "Did you give water to the dying man?",
                "gave_water",
                [Opt("Gave water to the dying man", "true"), Opt("Refused water", "false")],
                "Generosity"),
            Choice("s2", 2, "Who did you sit with at dinner?",
                "sat_with_kenny",
                [Opt("Sat with Kenny", "true"), Opt("Sat with Luke", "false")],
                "Loyalty"),
            Choice("s2", 2, "Told Walter the truth about Matthew?",
                "told_walter_truth",
                [Opt("Told Walter the truth", "true"), Opt("Didn't tell", "false")],
                "Honesty"),
            Choice("s2", 2, "Nick's fate?",
                "walter_forgave_nick",
                [Opt("Convinced Walter to forgive Nick", "true"), Opt("Let Walter decide", "false")],
                "Life/Death"),
            Choice("s2", 2, "Took blame for Sarah's photo?",
                "took_blame_sarah",
                [Opt("Took the blame", "true"), Opt("Blamed someone else", "false")],
                "Friendship"),
            Choice("s2", 3, "Told Bonnie about Luke?",
                "told_bonnie_luke",
                [Opt("Told Bonnie that Luke contacted you", "true"), Opt("Chose to hide Luke's presence", "false")],
                "Trust"),
            Choice("s2", 3, "Watched Kenny kill Carver?",
                "watched_kenny_kill_carver",
                [Opt("Watched", "true"), Opt("Left with Sarita", "false")],
                "Vengeance"),
            Choice("s2", 3, "Chopped off Sarita's arm?",
                "sarita_choice",
                [Opt("Chopped off Sarita's arm", "chopped"), Opt("Killed the zombie that bit Sarita", "killed")],
                "Risk"),
            Choice("s2", 3, "Helped Sarah with her chores?",
                "helped_sarah_chores",
                [Opt("Helped Sarah", "true"), Opt("Did your own work", "false")],
                "Compassion"),
            Choice("s2", 3, "Admitted to stealing the walkie talkie?",
                "admitted_walkie",
                [Opt("Tried to speak up", "true"), Opt("Tried to hide the theft", "false")],
                "Selflessness"),
            Choice("s2", 3, "Left to find Kenny?",
                "left_to_find_kenny",
                [Opt("Stayed to help Carlos", "true"), Opt("Sought Kenny's help", "false")],
                "Bravery"),
            Choice("s2", 4, "Left Sarah at the trailer park?",
                "saved_sarah",
                [Opt("Saved Sarah at the trailer park", "true"), Opt("Left Sarah behind", "false")],
                "Pragmatism"),
            Choice("s2", 4, "Robbed Arvo?",
                "robbed_arvo",
                [Opt("Stole pills from Arvo", "true"), Opt("Refused to steal from Arvo", "false")],
                "Compassion"),
            Choice("s2", 4, "Shot Rebecca?",
                "shot_rebecca",
                [Opt("Chose to shoot Rebecca", "true"), Opt("Did not shoot Rebecca", "false")],
                "Survivalism"),
            Choice("s2", 4, "Protected the baby?",
                "protected_baby",
                [Opt("Protected the baby", "true"), Opt("Went for cover", "false")],
                "Selflessness"),
            Choice("s2", 4, "Crawled through ticket booth window?",
                "crawled_through_window",
                [Opt("Volunteered to crawl through", "true"), Opt("Let Bonnie reach through", "false")],
                "Selflessness"),
            Choice("s2", 4, "Held the baby?",
                "held_baby",
                [Opt("Held the baby", "true"), Opt("Didn't hold the baby", "false")],
                "Nurturing"),
            Choice("s2", 4, "Asked to leave with Mike?",
                "asked_leave_mike",
                [Opt("Wanted to leave with Mike", "true"), Opt("Didn't ask to leave", "false")],
                "Loyalty"),
            Choice("s2", 5, "Went to help Luke?",
                "helped_luke_ice",
                [Opt("Tried to help Luke", "true"), Opt("Did not try to help", "false")],
                "Risk"),
            Choice("s2", 5, "Shot Kenny?",
                "shot_kenny",
                [Opt("Shot Kenny", "true"), Opt("Didn't shoot Kenny", "false")],
                "Survivalism"),
            Choice("s2", 5, "In the end, who are you with?",
                "s2_ending",
                [Opt("With Jane and the family", "jane_family"),
                 Opt("With Jane", "jane"),
                 Opt("With AJ at Wellington", "wellington"),
                 Opt("Alone with AJ", "alone"),
                 Opt("With Kenny", "kenny")],
                "Friendship"),
        ];
    }

    // ── Michonne (descriptions from game's choice.prop) ───────────────

    private static List<ChoiceDefinition> BuildMichonne()
    {
        return
        [
            // Sourced from WDC_pc_ProjectSeasonM_data.ttarch2/choice.prop
            Choice("michonne", 1, "Did you try to end it?",
                "pulled_trigger",
                [Opt("Pulled the trigger", "true"), Opt("Lowered the gun", "false")]),
            Choice("michonne", 1, "How did you enter the abandoned ferry?",
                "entered_ferry",
                [Opt("Climbed the ladder", "ladder"), Opt("Went in through the window", "window")]),
            Choice("michonne", 1, "Did you sell Greg out to Norma?",
                "sold_greg_out",
                [Opt("Shared the blame with Greg", "shared"), Opt("Told Norma Greg was a liar", "liar"),
                 Opt("Took the blame", "blame")]),
            Choice("michonne", 1, "Did you ambush Randall in the store room?",
                "ambushed_randall",
                [Opt("Head-butted Randall", "true"), Opt("Played it cool", "false")]),
            Choice("michonne", 1, "Did you let Sam shoot Zachary?",
                "sam_shot_zachary",
                [Opt("Let Sam take her revenge", "true"), Opt("Tried to save Zachary", "false")],
                "Life/Death"),
            Choice("michonne", 2, "Did you keep Pete with you, or let him go off on his own?",
                "stopped_pete",
                [Opt("Stopped Pete", "true"), Opt("Let Pete go", "false")]),
            Choice("michonne", 2, "Did you pick up the phone or go after the footsteps?",
                "picked_up_phone",
                [Opt("Picked up the phone", "true"), Opt("Went to see who was in the hallway", "false")]),
            Choice("michonne", 2, "How did you handle the radio call from Norma?",
                "radio_norma",
                [Opt("Ignored the radio", "ignore"), Opt("Spoke with her directly", "spoke"),
                 Opt("Made Randall speak to her", "randall")]),
            Choice("michonne", 2, "What did you do to Randall?",
                "killed_randall",
                [Opt("Bashed Randall's head in", "true"), Opt("Showed him mercy", "false")],
                "Life/Death"),
            Choice("michonne", 2, "Did you let Sam bury her father?",
                "let_sam_bury",
                [Opt("Helped Sam move her father's body", "true"), Opt("Didn't help", "false")]),
            Choice("michonne", 3, "Did you hand Randall over to Norma?",
                "handed_randall_norma",
                [Opt("Handed Randall over", "true"), Opt("Did not hand over", "false")]),
            Choice("michonne", 3, "Did you put Norma out of her misery?",
                "shot_norma",
                [Opt("Shot Norma to put her out of her misery", "true"),
                 Opt("Let Norma get killed by walkers", "false")],
                "Life/Death"),
            Choice("michonne", 3, "Did you tell Alex what happened to his father?",
                "told_alex_father",
                [Opt("Told Alex his father couldn't come right away", "later"),
                 Opt("Stayed silent", "silent"), Opt("Told Alex his father was dead", "dead"),
                 Opt("Told Alex a bad man hurt his father", "hurt")]),
            Choice("michonne", 3, "Did you reveal to Paige that you nearly committed suicide?",
                "revealed_to_paige",
                [Opt("Expressed sympathy for the kids", "sympathy"), Opt("Remained silent", "silent"),
                 Opt("Disclosed your darkest moment", "disclosed"), Opt("Tried to offer good advice", "advice")]),
            Choice("michonne", 3, "Did you choose to leave your daughters or stay with them?",
                "stayed_with_daughters",
                [Opt("Chose to stay with your daughters", "true"), Opt("Chose to leave your daughters", "false")],
                "Life/Death"),
        ];
    }

    // ── Season 3: A New Frontier (descriptions from game's choice.prop) ──

    private static List<ChoiceDefinition> BuildSeason3()
    {
        return
        [
            // Sourced from WDC_pc_ProjectSeason3_data.ttarch2/choice.prop
            Choice("s3", 1, "Did you stay the night at the junkyard?",
                "stayed_junkyard",
                [Opt("Chose to stay the night", "true"), Opt("Chose to head back out on the road", "false")]),
            Choice("s3", 1, "Did you shoot the driver or let him go?",
                "shot_driver",
                [Opt("Chose to shoot the driver", "true"), Opt("Chose to let the driver go", "false")]),
            Choice("s3", 1, "What was the aftermath of the shooting?",
                "shooting_aftermath",
                [Opt("Got locked up", "locked"), Opt("Were allowed to roam free", "free")]),
            Choice("s3", 1, "Who brought you to the junkyard?",
                "who_brought_junkyard",
                [Opt("Went with Eleanor", "eleanor"), Opt("Went with Tripp", "tripp")]),
            Choice("s3", 1, "Did you escape with your family or stay with Clementine?",
                "escaped_or_stayed",
                [Opt("Stayed with Clementine", "true"), Opt("Escaped with your family", "false")]),
            Choice("s3", 2, "How did you deal with Conrad's threat to Clementine?",
                "shot_conrad",
                [Opt("Killed Conrad", "true"), Opt("Let Conrad capture Clementine", "false")],
                "Life/Death"),
            Choice("s3", 2, "Did you trust Jesus?",
                "trusted_jesus",
                [Opt("Took him at his word", "true"), Opt("Tied his hands together", "false")]),
            Choice("s3", 2, "How did you handle the New Frontier at the gates of Prescott?",
                "prescott_response",
                [Opt("Tried to negotiate", "negotiate"), Opt("Opened fire", "fire"),
                 Opt("Surrendered", "surrender")]),
            Choice("s3", 2, "How did you deal with David and Kate's argument?",
                "david_kate_argument",
                [Opt("Told David that Kate wanted to leave him", "true"), Opt("Stayed out of it", "false")],
                "Relationship"),
            Choice("s3", 3, "Did you try to save AJ?",
                "gave_aj_medicine",
                [Opt("Injected AJ with the medicine", "true"), Opt("Didn't risk using the medicine", "false")]),
            Choice("s3", 3, "How did Badger die?",
                "badger_fate",
                [Opt("Let Badger turn", "turn"), Opt("Let someone else kill Badger", "someone_else"),
                 Opt("Killed Badger quickly", "quick"), Opt("Destroyed Badger's skull", "destroyed")],
                "Life/Death"),
            Choice("s3", 3, "Did you honor your brother's request?",
                "honored_brother",
                [Opt("Demanded justice for Mariana's murder", "true"), Opt("Kept Mariana's murder to yourself", "false")]),
            Choice("s3", 3, "How far did you go to get into Richmond?",
                "richmond_entry",
                [Opt("Capitulated to Max's demands", "true"), Opt("Didn't capitulate", "false")]),
            Choice("s3", 3, "What was Max's fate?",
                "max_fate",
                [Opt("Killed Max or watched David do it", "killed"), Opt("Brought Max back with you", "spared")],
                "Life/Death"),
            Choice("s3", 4, "How did you respond to Dr. Lingard's request?",
                "lingard_fate",
                [Opt("Let Clementine decide", "clem"), Opt("Assisted in Lingard's suicide", "assisted"),
                 Opt("Refused to kill Lingard", "refused")],
                "Life/Death"),
            Choice("s3", 4, "Did you promise to help Kate with the family if David left?",
                "promised_kate",
                [Opt("Told David it was his responsibility", "false"), Opt("Promised you'd help Kate", "true")],
                "Relationship"),
            Choice("s3", 4, "Did you tell Kate that you have feelings for her?",
                "told_kate_feelings",
                [Opt("Didn't share Kate's feelings", "false"), Opt("Told Kate you shared her feelings", "true")],
                "Relationship"),
            Choice("s3", 4, "Who did you try to save at the execution?",
                "trippava_saved",
                [Opt("Tried to save Ava", "ava"), Opt("Tried to save Tripp", "tripp")],
                "Life/Death"),
            Choice("s3", 4, "Did you shoot Joan or take Clint's deal?",
                "shot_joan",
                [Opt("Chose to shoot Joan", "true"), Opt("Chose to take the deal", "false")]),
            Choice("s3", 5, "What did you say to David about Kate?",
                "david_about_kate",
                [Opt("Denied having a relationship", "denied"), Opt("Said nothing", "nothing"),
                 Opt("Confessed your love for Kate", "confessed"), Opt("Came clean about relationship", "clean")],
                "Relationship"),
            Choice("s3", 5, "Did you fight David back?",
                "fought_david",
                [Opt("Fought back", "true"), Opt("Showed your love for David", "false")]),
            Choice("s3", 5, "Did you stand with David on the ledge?",
                "stood_with_david",
                [Opt("Stepped up to help David", "true"), Opt("Didn't stand with him", "false")]),
            Choice("s3", 5, "Who did you side with in the end?",
                "s3_ending",
                [Opt("Chose to leave with Kate", "kate"), Opt("Stuck to David's plan", "david")],
                "Life/Death"),
            Choice("s3", 5, "Did you go after Gabe or with Kate?",
                "went_after_gabe",
                [Opt("Went after Gabe", "gabe"), Opt("Went with Kate and sealed the breach", "kate")],
                "Life/Death"),
        ];
    }

    // ── Season 4: The Final Season (descriptions from game's choice.prop) ──

    private static List<ChoiceDefinition> BuildSeason4()
    {
        return
        [
            // Sourced from WDC_pc_ProjectSeason4_data.ttarch2/choice.prop
            Choice("s4", 1, "Hunting vs Fishing",
                "fishing_or_hunting",
                [Opt("Went fishing with Violet and Brody", "fishing"),
                 Opt("Went hunting with Louis and Aasim", "hunting")]),
            Choice("s4", 1, "Abel Food",
                "surrendered_food_abel",
                [Opt("Surrendered food to Abel", "true"), Opt("Attacked Abel rather than giving him food", "false")]),
            Choice("s4", 1, "AJ Bed",
                "aj_bed",
                [Opt("Let AJ sleep under the bed", "under"), Opt("Convinced AJ to sleep on the bed", "on")]),
            Choice("s4", 1, "Violet vs Marlon",
                "turned_to_for_help",
                [Opt("Turned to Violet for help against Marlon", "violet"),
                 Opt("Turned to Louis for help against Marlon", "louis")]),
            Choice("s4", 1, "Happy Couple",
                "happy_couple",
                [Opt("Killed the walker couple in the train station", "killed"),
                 Opt("Let AJ go through the window", "window")]),
            Choice("s4", 2, "AJ's Gun",
                "ajs_gun",
                [Opt("Gave AJ's gun to Louis", "gave"), Opt("Told AJ to keep his gun", "kept")]),
            Choice("s4", 2, "Violet Run or Shoot",
                "violet_run_shoot",
                [Opt("Told Violet to shoot Lilly, got Louis shot", "shoot"),
                 Opt("Told Louis and Violet to run", "run")]),
            Choice("s4", 2, "Follow Violet or Louis",
                "follow_violet_louis",
                [Opt("Spent time stargazing with Violet", "violet"),
                 Opt("Helped Louis tune the piano", "louis")]),
            Choice("s4", 2, "Save Violet or Louis",
                "violetlouis_saved",
                [Opt("Rescued Louis instead of Violet", "louis"),
                 Opt("Rescued Violet instead of Louis", "violet")],
                "Life/Death"),
            Choice("s4", 3, "Zombie Abel",
                "mercy_killed_abel",
                [Opt("Mercy killed Abel", "true"), Opt("Forced Abel to turn into a walker", "false")]),
            Choice("s4", 3, "Redirect or Kill Zombie",
                "killed_james_walkers",
                [Opt("Let James throw the rock to distract", "distract"),
                 Opt("Killed the walker after James asked you not to", "killed"),
                 Opt("Spared the walker, honoring James's request", "spared")]),
            Choice("s4", 3, "AJ Attack Dorian",
                "aj_attack_dorian",
                [Opt("Stopped AJ and let Dorian cut off your friend's finger", "stopped"),
                 Opt("Allowed AJ to attack Dorian", "allowed")]),
            Choice("s4", 3, "AJ Shoot Lilly",
                "killed_lilly",
                [Opt("Told AJ to kill Lilly", "true"), Opt("Refused to tell AJ to kill Lilly", "false")],
                "Life/Death"),
            Choice("s4", 3, "Forest Walker Kills",
                "forest_walker_kills",
                [Opt("Ignored James' wishes and killed all walkers in camp", "all"),
                 Opt("Respected James' beliefs and killed no walkers", "none"),
                 Opt("Only killed some of the walkers", "some")]),
            Choice("s4", 4, "Trusted AJ",
                "trusted_aj",
                [Opt("Trusted AJ to make hard calls", "true"), Opt("Did not fully trust AJ", "false")]),
            Choice("s4", 4, "Bomb Name",
                "bomb_name",
                [Opt("Named the bomb 'Willy Junior'", "willy"), Opt("Named the bomb 'AJ'", "aj"),
                 Opt("Named the bomb 'Mitch's Masterpiece'", "mitch"), Opt("Refused to name the bomb", "none"),
                 Opt("Named the bomb 'Ruby's Revenge'", "ruby")]),
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
