# A New Frontier (Season 3)

A Season 3 slot is `wd3_saveslot<N>.bundle` (slot metadata only), one `_wd3_saveslot<N>_autosave.bundle`, and the event log `_wd3_saveslot<N>_id.estore` with its pages. The game's checkpoints carry no chapter ids in this season, so there is a single save per slot.

The game scripts are the same framework as Season 2 and the editor shares the log handling with it (`Season.Base/DialogLog`). What is specific to this generation of the framework is in `Season.Base/Story` and is shared with [Michonne](michonne.md) and [Season 4](season-4.md); each of the three is described by a `StorySeason` profile (project and metadata names, number of episodes, date format, and the differences noted in their pages).

## The log

Besides dialog nodes and save serials the log holds `Begin Episode` and `End Episode` markers, dialog choice events, and at its start the block `Previous Game Data Begin` … `Previous Game Data End`: every dialog node of the Season 2 save that was imported, or of the story the player built instead. The game lists a slot as empty when its log has no such block, so the editor always writes one. Restarting an episode cuts the log at that episode's `Begin Episode` marker, as `EventLog_TruncateEpisode` does. The page that holds most of the imported block (about 9,500 events) is stored compressed.

## Decisions

`persistent.prop` lists the story keys each episode reads, as expressions over node ids with `|`, `&`, `~` and parentheses; eight of them are Season 2 outcomes read from the imported block. `choice.prop` lists the end-of-episode statistics choices the same way. `NodeExpression` is a port of the game's own evaluator (left to right, no operator precedence, short-circuiting), and a test checks that it reproduces every value the game stored in a real save.

The decision list is `Data/s3.decisions.json`, built by `ExtractDecisions 3`: a story key becomes part of a statistics choice when each of its values lies inside a different option of that choice, otherwise it gets its own row, so 49 rows cover 25 statistics choices and 34 story keys. Rows marked Statistics only change the summary screen.

To set an option the editor searches the node combinations of that decision for one that makes the option true and the others false, preferring the one that changes the fewest other decisions, then rewrites, removes or inserts nodes: Season 2 outcomes inside the imported block, others before the next episode's `Begin Episode`, the episode's `End Episode`, or the latest `Save Serial`. Story keys already stored in the save are re-evaluated after every change (`StoryChoiceAccessor.UpdateSavedLogic`), but only those the save's episode reads.

## Skipped episodes

The game fills in the decisions of episodes that were not played from one of two prebuilt logs (`generatedLog10<N>A/B.estore`, chosen by `Generated Choices ID`) and unions them with the slot's log. The editor instead writes every earlier decision into the slot's log, adds `Begin Episode`/`End Episode` markers for the earlier episodes, sets `Last Episode Finished` and the `Completed Episode <N>` flags, and clears `Episodes Skipped`, so the game neither offers to randomise nor mixes in a prebuilt log.

## Chapters

As in Season 2, each episode's `Episode.lua` has the developers' chapter menu, and a save whose `logic_script` holds `Script - Previous` = `DebugMenu` makes the scene script set itself up. The generated autosave holds that marker, the entry's flags, and every story key the episode reads, computed from the log, because `PersistentLogic_SetGameLogic` is skipped under the marker. A menu entry that sets a story key, such as the four Episode 1 flashbacks that each belong to one Season 2 ending, also sets that decision in the log.

Two Episode 1 entries ("Garcia House - Credits" and "Junkyard Hill - Trailer") are left out: their scene scripts only register the setup that reads the entry's flag in developer builds, so in the released game they would start the scene from its beginning. Five more entries whose scene setup only runs in debug builds are dropped by the same rule that Season 4 needed, and Episode 4's "Richmond Square Dawn Action" is dropped because it repeats "Richmond Square Dawn" exactly: the same script and the same `Act` = 3, and the menu never passes the button's label on. The 79 resume points are `Data/s3.chapters.json`, from `ExtractResumePoints 3`.

## Inventory

Only Episodes 1 and 2 have items, four each, registered by `Inventory_InitItem` in `Episode.lua`. An item is an integer `Inventory - <name>` on the `logic_inventory` agent, set by logic rules in the dialogs rather than by script calls. `Inventory.lua` keeps a copy per player character and restores the shared set from it whenever the player character is set, so a save holds the counts twice, in `logic_inventory` and in `logic_inventory_Javier`; the editor writes both.

"Add items picked up earlier" uses the same rule as Season 2, with the dialog rules as its source: Junkyard Hill starts with the crowbar and the siphon, the truck scenes with the candy bar, and the Episode 2 scenes up to the car with the water bottle. Where giving an item away is a choice (the candy bar, the tape player) the item is taken as given once that scene is over. The items are `Data/s3.items.json`, from `ExtractResumePoints 3`.

## Import

"Import" on a Season 3 save copies the dialog nodes of a Season 2 save's log into the imported block, which is what the game's own import does.

## Checked in game

Restarts of every episode, chapter checkpoints and item changes were loaded in the game; the decisions set in the editor showed up in the recap and in the stats screen.
