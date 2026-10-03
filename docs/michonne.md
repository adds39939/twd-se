# Michonne

Michonne's scripts are the earlier version of the Season 3 framework, so both seasons run on the same code (`Season.Base/Story`). Read [season-3.md](season-3.md) first; this page is the differences. A slot is `wdm_saveslot<N>.bundle`, the autosave, one `_wdm_saveslot<N>_checkpoint<K>.bundle` per chapter as in Season 2, and the event log.

## No earlier game

There is no imported block; the log starts with `Begin Episode 1`.

## Decisions

`persistent.prop` and `choice.prop` have the Season 3 layout and `ExtractDecisions m` builds 36 rows from them (`Data/michonne.decisions.json`). A story key can be listed for both Episode 2 and Episode 3, in two cases with different expressions; it is one row, and setting it searches for the node combination under which every definition reads the same.

Michonne's evaluator does not strip braces from a node id, so the one key written with braces (`Greg Zombified`) is always false in the game and is not offered. The evaluator's results match every story key stored in the three sample saves.

## Empty slots

As in Season 2 the menu lists a slot without any save bundle as empty. A new save, or a restart that would leave no save, therefore gets a checkpoint that runs the episode's first script without the developer-menu marker, which starts the episode normally. `Last Episode Finished` is a string in real saves and is written as one.

## Chapters

The developer menu call takes a page and a position (`DebugMenu_AddButton(1, 2, "Cove Prologue", "ShoreLineCove", …)`), and Episode 1 lists its scenes a second time under their dialog file names; an entry named after a dialog is dropped when the same script and flags were already listed.

That second list also names two scenes (`FlagshipExteriorEscape`, `BoatTownEscape`) whose scripts are still in the Episode 1 archive but whose scene and dialog files are not: they were moved to Episode 2, and a checkpoint for them never finishes loading. `ExtractResumePoints` therefore drops every menu entry whose scene file is not in the episode's archive (none are affected in Seasons 2 and 3). This gives 54 resume points (`Data/michonne.chapters.json`), each with the game's chapter id (`101_chapter4`) for the checkpoint.

## Inventory

Items are integers on `logic_inventory` alone, registered in `Episode.lua` and changed by `Inventory_AddItem` and `Inventory_RemoveItem` calls in dialog scripts. Only Episode 1 hands any out: machete, binoculars and flashlight on the boat (all taken away on arrival at Monroe), the rebar within one scene, and the screwdriver during the escape, the episode's last scene. Items that are registered but that nothing ever gives (the map, and all of Episode 3's) are not listed. `StoryInventory` serves both this season and Season 3, which differ only in the property sets written.

## Checked in game

All three test slots loaded and played; the hang on the two moved scenes is what led to the archive check above.
