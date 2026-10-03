# Save formats

The Definitive Series keeps every season's saves in `Documents\Telltale Games\The Walking Dead Definitive`. All of them are built from the same handful of Telltale containers; the seasons differ in which files a save has and what the game puts in them.

## Files per season

| Season | Prefix | Files of one slot |
|--------|--------|-------------------|
| Season 1 | `wd1_` | `wd1_saveslot<N>.bundle`, `_wd1_saveslot<N>_autosave.bundle` |
| Season 2 | `wd2_` | slot bundle, `_wd2_saveslot<N>_checkpoint<K>.bundle` per chapter, `_wd2_saveslot<N>_autosave.bundle`, `_wd2_saveslot<N>_id.estore` and its `_id_Page<id>.epage` pages |
| Michonne | `wdm_` | as Season 2 |
| Season 3 | `wd3_` | slot bundle, one autosave, estore and pages |
| Season 4 | `wd4_` | as Season 3 |

The editor lists the slot bundle and loads the rest with it as companion files. Before writing anything it copies every file it is about to touch into a `backup_<timestamp>` folder.

The folder also holds files that belong to the game rather than to a slot, and the editor leaves them out of the list: only bundles a season recognises by their prefix are read. `global.bundle` holds `achievement_status.prop`, a copy of which achievements are unlocked that `Achievements.lua` rewrites from the platform's own record, so editing it would change nothing that lasts. `menu.bundle` holds `viewed_entries.prop`, the episodes whose "new" badge in the chapter menu has been seen (`ViewTracker.lua`). `prefs.prop` and `ttcd.prop` are the game's settings, and the `menu_log_*` and `session_*` estores are event logs the game keeps for itself.

## MetaStream

Every file is a MetaStream, Telltale's container. The header is the magic (`MSV6`, or `MSV5` in a few old files), a list of version entries (type CRC64 + version CRC32) and three section sizes: default, debug and async. A section whose size has the top bit set is TTCZ compressed: 64 KiB pages of raw deflate behind a page offset table, and the data is zero-padded to whole blocks. The debug section holds four zero bytes for every symbol in the default section.

The readers treat every file as untrusted: a length that runs past the end of its data, a property set nested more than 16 deep or a section that inflates past 64 MiB makes the file unreadable, and every such failure surfaces as `InvalidDataException`, so one bad file cannot crash the app or stop the rest of the folder from loading.

## Property sets

A `.prop` is a typed key-value store, version 2. Keys are CRC64 symbols, values are grouped by type and ordered by hash. The types the saves use are bools, ints, floats, strings, symbols, handles, and `DCArray<String>` (a count followed by length-prefixed strings). A set can list parent sets (`metadata_slot_s1.prop` and the like); flag `0x100` means the keys are local.

The runtime sets in checkpoints also hold engine types. Their layouts are in `Core.Binary/Data/property-value-layouts.json`: plain structs of floats or symbols have a fixed size (`Color`, `Vector3`, `PhonemeKey`), every `Handle<T>` is the 8-byte symbol of the resource and counts as a symbol in the debug section, classes such as `LocationInfo`, `AnimOrChore`, `SoundEventName<N>` and `ScriptEnum:<name>` write their members in blocks that start with their own size, and the maps and arrays are a count followed by fixed-size entries. A type that is not in the list makes the set unreadable instead of being guessed; with these layouts every property set in the test saves reads and writes back byte for byte.

## Bundles

A `.bundle` is an outer MetaStream whose default section is a table of 40-byte entries (offset, size, 16-byte name, name symbol, type symbol) and whose async section holds the inner MetaStream files. The first two entries are named (`metadata_save.prop` and `default.save`); everything after is named by symbol only. A game-written autosave holds thousands of these, one property set per agent in the scene.

Runtime property sets are named by the hash of `"<agent>:<scene file>" Runtime Properties`, for example `"logic_game:module_logic.scene" Runtime Properties`; the scene agent itself is `"adv_x.scene:adv_x.scene"`. This was found in the decompiled engine and checked against real saves.

`default.save` names the script that is run on load, the resource sets to enable, the agent transforms, and the list of runtime property names to restore. See [engine.md](engine.md) for what the engine does with it.

## Event logs

Seasons 2 to 4 and Michonne keep a log of what was played. `EventStorage` (the `.estore`) holds the page list, the last event id and the current unflushed page; each `EventStoragePage` (`.epage`) holds its events. An event is an id, a severity and a block of typed data: a type symbol and values that are symbols, integers or doubles (save serials are doubles). Ids rise through the log but are neither contiguous nor strictly ordered.

If the `.estore` or one of the pages it lists cannot be read, the save is marked as having a damaged dialog log and nothing that writes the log is allowed: its decisions are not shown, restarting and the inventory are refused, and saving leaves the log files as they are.

Event types that matter:

| Event | Meaning |
|-------|---------|
| `Executing Dialog Node` | A dialog node ran; the value is the CRC64 of the node's `{GUID}` (braces included) |
| `Dialog Choice` | The player picked an option |
| `Save Serial` | A save was written |
| `Begin Episode` / `End Episode` | Episode markers (Season 3 onwards) |
| `Previous Game Data Begin` / `End` | Brackets the nodes imported from the previous season |

A page that holds a large imported block (about 9,500 events in a Season 3 save) is stored compressed; `EventLogCodec` reads and writes those.

## Hashes

Names are hashed with Telltale's CRC64 (the ECMA-182 polynomial, with the string lowercased first). `TelltaleHash.ComputeCrc64` does it, and the `Symbols` tool hashes a string or looks a hash up against the name lists in `tools/data/names`. Property names in the Properties tab come from `PropertyNameDb`, which is seeded with every choice key and the names each season handler contributes.

## Where decisions live

| Season | Storage |
|--------|---------|
| Season 1 | `Persistent - <episode> - <key>` strings in the slot's `metadata_slot.prop`, mirrored in the autosave's game logic and in the `choices.prop` stats tracker |
| Season 2 | Dialog node events in the log; the imported Season 1 values as strings in the slot's `season1.prop` |
| Season 3 | Dialog node events in the log, including the imported Season 2 block |
| Season 4 | Dialog node events in the log, including the imported Season 3 block and the story builder's answers |
| Michonne | Dialog node events in the log |

The decision definitions come from the game's own files: `persistent.prop` (story keys and the expressions that compute them), `choice.prop` and `statsInfo_*.prop` (end-of-episode statistics).
