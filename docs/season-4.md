# The Final Season (Season 4)

The Final Season runs the Season 3 framework with a few changes, so it is the third season on `Season.Base/Story`. Read [season-3.md](season-3.md) first. A slot is `wd4_saveslot<N>.bundle`, one autosave and the event log. The slot bundle's `choicestats.prop` only records which statistics the player has already seen on the stats screen; the decisions themselves are in the log.

## Two imported blocks

The log holds two `Previous Game Data` blocks: `Local Save`, the dialog nodes of the imported Season 3 log (which already contains the Season 2 nodes Season 3 imported), and `configuratorResults`, the answers of the story builder that opens Episode 1. The seven rows under "Carried over from the previous season" (Lee, Kenny, Doug, Lilly and the Season 2 ending) read from both; a change is written into the first block, where Season 4 reads it.

## Decisions and chapters

Story keys and chapters come from the same `persistent.prop`, `choice.prop` and `Episode.lua` layouts (`ExtractDecisions 4`, `ExtractResumePoints 4`): 66 rows and 67 resume points. Untitled statistics (Episode 4) take their first option as the title.

The developer menu lists each act twice, the second time with jumps to points inside a scene; those pages are merged into story order, and the entries whose jump only works in debug builds (`if IsDebugBuild() then Callback_OnLogicReady:Add(OnLogicReady)`) are dropped. Applying that rule to every season also removed five such entries from Season 3.

## Save layout

The script and save-system properties live on one agent, `logic_systems`, instead of `logic_script` and `logic_saveload`; `Last Episode Finished` is an integer; the game logic set is marked visible. The game evaluates the story keys once per episode and remembers that in `Persistent - Game Logic Is Set`; a chapter save written here starts under the developer-menu marker, so the game keeps the values the save carries.

## Loaded scenes resume, never enter

Season 4's `Game.lua` takes the engine's `loaded` flag at face value: a scene opened from a save runs only the checkpoint dialog stored in `SaveLoad - Checkpoint Dialog File` and `Node`, not its own entry sequence (earlier seasons compared the script against `Script - Current` instead). The first chapter saves built without this soft-locked on a black screen.

A chapter save therefore carries the scene's entry dialog and node, read by `ExtractResumePoints` from `adv_<scene>.prop`: `Scene - Dialog`, a handle that is the CRC64 of the dialog file name, and `Scene - Dialog Node`, by default `cs_opening` from `scene.prop`. The dialog is switched to the `_act<N>` dialog named by the entry's `Act` flag, and the node to the one a flag selects in the scene script. Entries without a resolvable dialog are dropped (two at McCarroll Ranch), leaving 67.

## Skipped episodes

Filled by copying the generated logs into the slot's log (`SaveLoad_CopyGeneratedSave`), which is what the editor's restart writes, so no randomise prompt appears.

## Story builder

Episode 1's first script is the story builder, which asks about the earlier seasons and appends its answers as a second block. Starting Episode 1 from its beginning therefore asks again; resuming from "Road Tile" keeps what was set in the editor.

## Collectibles

Season 4 has no items to carry (`Inventory_*` is never called and no save holds a `logic_inventory` set); the Inventory tab lists its collectibles instead. `Collectible.lua` names them per episode, and the slot file keeps `Collectible Found - <name>` and `Collectible Placed - <name>` booleans, which `ExtractResumePoints 4` turns into `Data/s4.items.json` and the tab shows as two entries per collectible. They need no save: the slot alone is edited.

## Checked in game

Restarts, chapter saves (after the entry-dialog fix) and collectibles were all confirmed in the game.
