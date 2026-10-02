# TWD: Telltale Definitive Series - Save Editor

A save editor for **The Walking Dead: The Telltale Definitive Series**. Edit choices, metadata, and resume points across all seasons — directly in your browser.

**Live app: [twd-se.app](https://twd-se.app/)**

![Blazor WASM](https://img.shields.io/badge/Blazor-WASM-512bd4)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4)
![Tests](https://github.com/adds39939/twd-se/actions/workflows/test.yml/badge.svg)

## Features

- **All 6 seasons supported** — Season 1, 400 Days, Season 2, A New Frontier, The Final Season, and Michonne
- **Native format editing** — each season's save format is handled natively (no hacks or file injection)
- **Choice editing** — change any tracked decision via labeled dropdowns
- **Metadata editing** — playtime, episode progress, autosave references, game completion
- **Resume point editing** — restart a Season 1, 2, 3 or Michonne save from the beginning of any episode, or from a chapter inside any episode including 400 Days, with the decisions you picked
- **Inventory editing** — choose what Lee (Season 1), Clementine (Season 2), Javier (Season 3) or Michonne carries in a save, or add the items picked up earlier in the episode after resuming from a chapter
- **Cross-season cascade and import** — optionally propagate Season 1 choice changes into Season 2 saves, and import a Season 1 save into Season 2 or a Season 2 save into Season 3 the way the game does
- **S4 presets** — quick-apply "Save Louis", "Save Violet", or "Trust AJ" choice paths
- **New save creation** — create saves for any season with pre-populated choices; Season 1 saves start at the episode you choose
- **File System Access API** — read/write directly to your save directory (Chromium-based browsers)
- **Upload fallback** — file upload + download for browsers without directory access

## How It Works

Each season stores choices differently. The editor handles all five formats natively:

| Season | Format | Storage |
|--------|--------|---------|
| Season 1 | `metadata_slot.prop` | `Persistent - <episode> - <key>` strings in the slot bundle, mirrored in the autosave's game logic |
| Season 2 | EventLog + `season1.prop` | Dialog node events in the slot's estore/epage files; imported Season 1 values in the slot bundle |
| Season 3 | EventLog | Dialog node events in the slot's estore/epage files, including the imported Season 2 block |
| Season 4 | `choicestats.pro` | Tab-separated GUIDs in bundle |
| Michonne | EventLog | Dialog node events in the slot's estore/epage files |

Choice definitions are sourced from the game's own data files (`persistent.prop`, `statsInfo_*.prop` and `choice.prop`).

### Season 1 saves

A Season 1 save is two files, and the editor treats them as one save:

| File | Contents |
|------|----------|
| `wd1_saveslot<N>.bundle` | `metadata_slot.prop` (episode in progress, progress, completed episodes, latest autosave and every persistent decision) and `choices.prop` (the per-episode stats tracker) |
| `_wd1_saveslot<N>_autosave.bundle` | `metadata_save.prop` (episode, checkpoint and date), `default.save` and one property set per runtime object, including the game logic that holds a copy of the decisions |

When an episode starts the game copies the decisions of the earlier episodes from the slot into its game logic, and the autosave stores that logic. Changing a decision therefore updates three places: the persistent value in the slot, the stats tracker entry, and the game logic inside the autosave.

**Resume point.** The checkpoint the game writes is a complete snapshot of the running episode (thousands of per-agent property sets), so it cannot be moved to another scene. The editor replaces it instead.

*From the start of an episode:* the editor rewinds the slot the way the game's own rewind does, fills in any decision of the earlier episodes that is not set, and removes the autosave. The game then starts that episode from its first scene with your decisions. Until its first checkpoint the slot is listed as a new game: select it, pick the episode and press Play.

*From a chapter:* each episode's `WDEpisode.lua` contains the developers' chapter menu, which lists the game-logic flags and inventory items a scene needs before `LoadScript` starts it. The editor writes a small autosave that reproduces such a jump:

- `default.save` names the script of the scene *before* the chapter and the property sets that follow
- the game-logic set holds the decisions made so far, the chapter's flags, and the flags or inventory items that mirror a decision of the current episode once that decision is behind the chapter
- the checkpoint set's `Checkpoint Dialog Item` is `dlg_id: <node id>`, the node of that scene's dialog that runs `LoadScript` for the chapter's scene
- the scene agent's set forces `Dialog Agent - File Primary` to the dialog file that contains the node

On loading, the game opens the earlier scene, runs that one node, and starts the chapter's scene the normal way, so the scene's own setup runs and the game writes a full checkpoint of its own. Chapters inside the first scene of an episode go through `PreviouslyOn.lua` instead, which loads the first scene after the recap. The chapter list is `Data/s1.chapters.json`, generated by the `ExtractChapters` tool.

400 Days has five stories that can be played in any order from a hub, which keeps `N - Complete` and `Last Chapter` in its game logic and hides the board items of finished stories. The editor's chapter list assumes the stories are played in order: a chapter counts every story listed before it as finished, sets those flags, hides those board items, and carries each finished story's decision together with the flag that mirrors it. The hub is offered after each story as well, and the epilogue counts all five as finished.

Runtime property sets in an autosave are named by the hash of `"<agent>:<scene file>" Runtime Properties`, for example `"logic_game:module_logic.scene" Runtime Properties`.

Decisions of the episode that is in progress are written to the checkpoint as well. In a checkpoint written by the game, the scenes already played do not change; set the resume point again if you want a decision to take effect from an earlier point.

**Inventory.** Each episode's `WDEpisode_InventoryInit` lists its items. An item is a number on the `logic_inventory_items` agent (`Inventory - Axe` = 1 when carried, 0 when not), kept in that agent's runtime properties in the autosave. Episode 1 also shows five values of the game logic as items: the office keys, the brick and the photo are flags, and the energy bars and batteries are counts. Dialog logic rules set these values, so the Inventory tab reads and writes them directly; a counted item gets a count selector, and its largest count is the number of rules that increase it.

For a chapter checkpoint written by the editor, "Add items picked up earlier" follows the developers' chapter list and gives only what is certain: an item counts as picked up once every visit to a scene whose dialogs hand it out lies before the chapter, and it is dropped if a scene that can take it away was visited since the first of those visits. A dialog belongs to the scenes that load it, reference it anywhere in their scene file, or are named like it. An item that a scene both gives and takes is kept only when a later chapter's developer setup hands it out. Season 1's items open puzzle steps and its chapter list revisits scenes out of order, so, unlike Season 2, an item that may not have been found yet or may already have been used is left out; set against the six real saves it leaves nothing out, matches three exactly and adds one item in the other three (the bandage at the end of Episode 1, the blowtorch in the Episode 3 epilogue, and the cleaver in the Episode 5 jewelry store, which that scene's script removes on entry). An item that is an option of a persistent decision is kept once it is handed out and swapped for the option chosen, which makes the Episode 3 weapon follow `Weapon Choice`. Changing a decision rebuilds a generated checkpoint and keeps its items. The item list and per-chapter lists are generated by `ExtractChapters` into `Data/s1.items.json`.

### Season 2 saves

A Season 2 slot is `wd2_saveslot<N>.bundle` plus companion files: one `_wd2_saveslot<N>_checkpoint<K>.bundle` per chapter reached, `_wd2_saveslot<N>_autosave.bundle`, and the event log `_wd2_saveslot<N>_id.estore` with its `_id_Page<max event id>.epage` pages. The editor lists the slot and loads the rest with it.

**Decisions.** Season 2 does not store its own decisions as values. The game logs every dialog node it plays (`Executing Dialog Node`, with the hash of the node's `{GUID}`) and every save (`Save Serial`). At the start of an episode it evaluates expressions over those node ids, taken from `persistent.prop`, and writes the results into its game logic, where they stay for the rest of the episode. Starting a later episode without a save runs the game's `ChoiceRandomizer`, which names one node per option of every decision and simply adds the chosen ones to the log. The editor does the same with the option you pick:

- a node of another option that is already in the log is rewritten in place to the chosen option's node, and any further ones are removed
- a decision that is not in the log yet is added just before the `Save Serial` event of the last save of that decision's episode, with an unused event id, so that it survives the log being cut back when a save is loaded
- saves of the episode in progress that already hold the derived flag (`Episode 201 - Rescue Choice` and the like) get the flag updated too

The 40 decisions and their node ids are generated by `ExtractDecisions 2` from `ChoiceRandomizer.lua`, `persistent.prop` and `choice.prop`.

**Resume point.** Restarting from an episode follows the game's own rewind: saves of that episode and later ones are removed, the slot points at the last save that is left, and the log is cut after that save's serial. Decisions of the earlier episodes are kept, and those that are missing are filled in so the game does not ask to randomise them.

`Episode in Progress` of the slot is `WalkingDead20<N>` while an episode is being played. When an episode ends the game saves the log, sets it to `ShowEndCredits<N>`, and after the credits to `EpisodeCompleteShowEpisode<N+1>`, or to `finished` after Episode 5. The editor reads the last three as the next episode from the beginning, or as a finished season.

The last save of an episode is not its end. Episode 5's final checkpoint (`205_chapter10`) is the start of the fight at the rest stop, so a save resting there has no value yet for the two decisions that follow it (shooting Kenny, and who Clementine ends up with); they only reach the log when the episode ends.

The game lists a slot with no save files as empty, so a slot always keeps at least one. When a restart would leave none (restarting Episode 1, or a newly created save), the editor writes a small checkpoint that runs the episode's `PreviouslyOn.lua`, which is how the game itself opens an episode.

**Chapters.** Each Season 2 episode has a developer chapter menu in its `Episode.lua`. An entry names a scene script and, for a point inside a scene, the logic flags to set first. The menu marks the jump by setting `Script - Previous` of the `logic_script` agent to `DebugMenu`, and every scene script checks for that marker to give itself the flags and items it would normally inherit. A chapter checkpoint written by the editor reproduces this: its script is the scene's script, and its runtime properties hold the marker, the entry's flags, the imported Season 1 values (`Episode 10x - <key>`) and the decisions of earlier episodes, which `PreviouslyOn.lua` would otherwise work out from the log. The save is labelled with the game chapter it falls in (`Saved Game Chapter ID`), so the game's own chapter menu shows it in the right place.

Decisions of the chosen episode are kept when the scene they are made in comes before the chapter, and cleared otherwise so they can be made again. The chapter list, the chapter id of each entry and those scene positions are generated by `ExtractResumePoints 2` into `Data/s2.chapters.json`.

**Inventory.** What Clementine carries is the string list `Items - Clementine` in the runtime properties of the `logic_inventory` agent of a save, next to one `<item> - Shown` flag per item. The game derives its `bGot<Item>` and `bHas<Item>` logic flags from that list when the scene sets the player, so the editor writes the list, the shown flags, and clears `bHas<Item>` for an item it takes away. The Inventory tab edits the save the slot resumes from; a slot that starts an episode from its beginning has none, because the episode's `PreviouslyOn.lua` hands out the starting items itself. Each episode has its own item definitions (`ui_item_<name>.prop`), so only that episode's items are offered.

A chapter checkpoint written by the editor starts with only what the scene's developer setup adds. "Add items picked up earlier" works out the rest from the game's scripts: an item counts as carried from the first scene that adds it until the last scene that removes it, in the order of the developer chapter menu, and a change that sits in the same dialog block as a chapter checkpoint is placed at that chapter. An item that a scene both adds and removes is kept only when a later scene's developer setup still hands it out. The starting items of an episode follow the conditions in `PreviouslyOn.lua` (the watch in Episode 2 needs `Episode 201 - Stole Watch`). Items found earlier in the same scene are left out, since that scene may not have reached them yet. Against the 55 checkpoints of a complete real playthrough this never misses an item it held and sometimes adds an optional one. The item names, starting items and per-chapter lists are generated by `ExtractResumePoints` into `Data/s2.items.json`.

**Event log files.** `EventStorage` holds the page list, the last event id and the current unflushed page; each `EventStoragePage` holds its events. An event is an id, a severity, and a block with typed data: a type symbol and values that are symbols, integers or doubles (save serials are doubles). Event ids rise through the log but are neither contiguous nor strictly ordered. The debug section holds four zero bytes per symbol.

### Season 3 saves

A Season 3 slot is `wd3_saveslot<N>.bundle` (slot metadata only), one `_wd3_saveslot<N>_autosave.bundle`, and the event log `_wd3_saveslot<N>_id.estore` with its pages. The game's checkpoints carry no chapter ids in this season, so there is a single save per slot. The game scripts are the same framework as Season 2 and the editor shares the log handling with it (`Season.Base/DialogLog`); what is specific to this generation of the framework is in `Season.Base/Story` and is shared with Michonne.

**The log.** Besides dialog nodes and save serials the log holds `Begin Episode` and `End Episode` markers, dialog choice events, and at its start the block `Previous Game Data Begin` … `Previous Game Data End`: every dialog node of the Season 2 save that was imported, or of the story the player built instead. The game lists a slot as empty when its log has no such block, so the editor always writes one. Restarting an episode cuts the log at that episode's `Begin Episode` marker, as `EventLog_TruncateEpisode` does. The page that holds most of the imported block (about 9,500 events) is stored compressed; a compressed section is zero-padded to whole 64 KiB blocks, which `EventLogCodec` reads and writes.

**Decisions.** `persistent.prop` lists the story keys each episode reads, as expressions over node ids with `|`, `&`, `~` and parentheses; eight of them are Season 2 outcomes read from the imported block. `choice.prop` lists the end-of-episode statistics choices the same way. `NodeExpression` is a port of the game's own evaluator (left to right, no operator precedence, short-circuiting), and a test checks that it reproduces every value the game stored in a real save. The decision list is built by `ExtractDecisions 3`: a story key becomes part of a statistics choice when each of its values lies inside a different option of that choice, otherwise it gets its own row, so 49 rows cover 25 statistics choices and 34 story keys. Rows marked Statistics only change the summary screen. To set an option the editor searches the node combinations of that decision for one that makes the option true and the others false, preferring the one that changes the fewest other decisions, then rewrites, removes or inserts nodes: Season 2 outcomes inside the imported block, others before the next episode's `Begin Episode`, the episode's `End Episode`, or the latest `Save Serial`. Story keys already stored in the save are re-evaluated after every change.

**Skipped episodes.** The game fills in the decisions of episodes that were not played from one of two prebuilt logs (`generatedLog10<N>A/B.estore`, chosen by `Generated Choices ID`) and unions them with the slot's log. The editor instead writes every earlier decision into the slot's log, adds `Begin Episode`/`End Episode` markers for the earlier episodes, sets `Last Episode Finished` and the `Completed Episode <N>` flags, and clears `Episodes Skipped`, so the game neither offers to randomise nor mixes in a prebuilt log.

**Chapters.** As in Season 2, each episode's `Episode.lua` has the developers' chapter menu, and a save whose `logic_script` holds `Script - Previous` = `DebugMenu` makes the scene script set itself up. The generated autosave holds that marker, the entry's flags, and every story key the episode reads (computed from the log, because `PersistentLogic_SetGameLogic` is skipped under the marker). A menu entry that sets a story key, such as the four Episode 1 flashbacks that each belong to one Season 2 ending, also sets that decision in the log. Two Episode 1 entries ("Garcia House - Credits" and "Junkyard Hill - Trailer") are left out: their scene scripts only register the setup that reads the entry's flag in developer builds, so in the released game they would start the scene from its beginning. The 85 resume points come from `ExtractResumePoints 3`.

**Inventory.** Only Episodes 1 and 2 have items, four each, registered by `Inventory_InitItem` in `Episode.lua`. An item is an integer `Inventory - <name>` on the `logic_inventory` agent, set by logic rules in the dialogs rather than by script calls. `Inventory.lua` keeps a copy per player character and restores the shared set from it whenever the player character is set, so a save holds the counts twice, in `logic_inventory` and in `logic_inventory_Javier`; the editor writes both. "Add items picked up earlier" uses the same rule as Season 2, with the dialog rules as its source: Junkyard Hill starts with the crowbar and the siphon, the truck scenes with the candy bar, and the Episode 2 scenes up to the car with the water bottle. Where giving an item away is a choice (the candy bar, the tape player) the item is taken as given once that scene is over.

**Import.** "Import" on a Season 3 save copies the dialog nodes of a Season 2 save's log into the block, which is what the game's own import does.

### Michonne saves

Michonne's scripts are the earlier version of the Season 3 framework, so both seasons run on the same code (`Season.Base/Story`), each described by a `StorySeason` (project and metadata names, number of episodes, date format and the differences below). A slot is `wdm_saveslot<N>.bundle`, the autosave, one `_wdm_saveslot<N>_checkpoint<K>.bundle` per chapter as in Season 2, and the event log.

- **No earlier game.** There is no imported block; the log starts with `Begin Episode 1`.
- **Decisions.** `persistent.prop` and `choice.prop` have the Season 3 layout and `ExtractDecisions m` builds 36 rows from them. A story key can be listed for both Episode 2 and Episode 3, in two cases with different expressions; it is one row, and setting it searches for the node combination under which every definition reads the same. Michonne's evaluator does not strip braces from a node id, so the one key written with braces (`Greg Zombified`) is always false in the game and is not offered. The evaluator's results match every story key stored in the three sample saves.
- **Empty slots.** As in Season 2 the menu lists a slot without any save bundle as empty. A new save, or a restart that would leave no save, therefore gets a checkpoint that runs the episode's first script without the developer-menu marker, which starts the episode normally. `Last Episode Finished` is a string in real saves and is written as one.
- **Chapters.** The developer menu call takes a page and a position (`DebugMenu_AddButton(1, 2, "Cove Prologue", "ShoreLineCove", …)`), and Episode 1 lists its scenes a second time under their dialog file names; an entry named after a dialog is dropped when the same script and flags were already listed. That second list also names two scenes (`FlagshipExteriorEscape`, `BoatTownEscape`) whose scripts are still in the Episode 1 archive but whose scene and dialog files are not: they were moved to Episode 2, and a checkpoint for them never finishes loading. `ExtractResumePoints` therefore drops every menu entry whose scene file is not in the episode's archive (none in Seasons 2 and 3). This gives 54 resume points, each with the game's chapter id (`101_chapter4`) for the checkpoint.

- **Inventory.** Items are integers on `logic_inventory` alone, registered in `Episode.lua` and changed by `Inventory_AddItem` and `Inventory_RemoveItem` calls in dialog scripts. Only Episode 1 hands any out: machete, binoculars and flashlight on the boat (all taken away on arrival at Monroe), the rebar within one scene, and the screwdriver during the escape, the episode's last scene. Items that are registered but that nothing ever gives (the map, and all of Episode 3's) are not listed. `StoryInventory` serves both this season and Season 3, which differ only in the property sets written.

## Using the App

Visit **[twd-se.app](https://twd-se.app/)** in a Chromium-based browser (Chrome, Edge, Brave, etc.), click **Open Save Directory**, and navigate to your save folder (see below). Select a save file to start editing, or create a new one.

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone and Run

```bash
git clone https://github.com/adds39939/twd-se.git
cd twd-se
dotnet run --project src/TwdSaveEditor.Web
```

Open `http://localhost:5163` in a Chromium-based browser (Chrome, Edge, Brave, etc.).

### Run Tests

```bash
# All tests
dotnet test

# Unit and integration tests only
dotnet test tests/TwdSaveEditor.Core.Tests

# Playwright E2E tests only
dotnet test tests/TwdSaveEditor.Playwright
```

### Publish

```bash
dotnet publish src/TwdSaveEditor.Web -c Release -o output
```

The static site is output to `output/wwwroot/`.

## Save Directory

The game stores saves in:

```
Documents\Telltale Games\The Walking Dead Definitive
```

Click **Open Save Directory** in the app and navigate there. The app reads all `.bundle`, `.estore`, and `.epage` files automatically. Every save is backed up into a `backup_<timestamp>` folder before it is written.

## Project Structure

```
src/
├── TwdSaveEditor.Core/             Models and hashing shared by everything
│   ├── Model/                      SaveSlot, PropertySet, property values, EventLogEntry
│   ├── Constants/                  Known metadata hashes, event types, resume point keys
│   ├── Hashing/                    CRC64 (Telltale's ECMA-182 variant)
│   ├── Database/                   Property name lookup
│   └── Serialization/              ISaveBundleSerializer abstraction
├── TwdSaveEditor.Core.Binary/      Binary format readers/writers
│   ├── Primitives/                 Binary reader/writer helpers
│   ├── MetaStream/                 MetaStream container
│   ├── PropertySets/               PropertySet and choices container codecs
│   ├── Compression/                TTCZ page compression
│   ├── Bundles/                    Bundle reader/writer, save factory
│   └── EventLog/                   Estore/epage reader, writer, creator
├── TwdSaveEditor.Season.Common/    Contracts a season implements
│   ├── Abstractions/               ISeasonHandler, ISeasonRegistry, IChoiceAccessor, capability interfaces
│   ├── Model/                      ChoiceDefinition, EpisodeInfo, ChoicePreset, CompanionFile
│   ├── Services/                   SeasonRegistry, BackupFileResolver
│   └── Extensions/                 CreateSave / GetScenes helpers
├── TwdSaveEditor.Season.Base/      Building blocks shared by season implementations
│   ├── Handlers/                   SeasonHandlerBase, PropChoicesSeasonHandler, StorySeasonHandler
│   ├── Accessors/                  SaveAccessor (choices.prop)
│   ├── DialogLog/                  Event log editing, companion files and node expressions (Seasons 2, 3, Michonne)
│   ├── Story/                      Decisions, resume points and checkpoints of Season 3 and Michonne
│   ├── EventLog/                   ChoiceNodeMap, EventLog file naming (older tools)
│   ├── Resources/                  Embedded choice/scene data loader
│   └── Services/                   SaveManager (disk-based)
├── TwdSaveEditor.Season.S1/        Season 1 and 400 Days (persistent decisions, autosave, episode and chapter resume)
├── TwdSaveEditor.Season.S2/        Season 2
├── TwdSaveEditor.Season.S3/        A New Frontier
├── TwdSaveEditor.Season.S4/        The Final Season
├── TwdSaveEditor.Season.Michonne/  Michonne
│   ├── Handlers/                   The season's ISeasonHandler
│   └── Data/                       Choice and scene data as embedded JSON (every season project has these)
├── TwdSaveEditor.Bootstrap/        Composition root: registers the seasons and the save serializer
├── TwdSaveEditor.UI/               Razor class library
│   ├── App.razor                   Root component and router
│   ├── Layout/ Pages/ Components/  Razor components with their scoped styles
│   ├── Services/                   SaveEditorService, SaveBackupService, FileSystemService
│   ├── Model/ Configuration/       Toast, AppInfo
│   └── wwwroot/                    Global styles, images, file system JS module
└── TwdSaveEditor.Web/              Blazor WASM host: Program.cs, index.html, manifest, service worker

tests/
├── TwdSaveEditor.Core.Tests/       Unit + integration tests
│   ├── Unit/                       Binary format, hashing, season handlers, metadata
│   ├── Integration/                Real save parsing, full create/edit/save cycles, game compatibility
│   └── Support/                    Shared test helpers
├── TwdSaveEditor.Playwright/       E2E browser tests
└── TestData/                       Representative save files for all seasons

tools/                              Console tools for game data extraction and validation
├── TwdSaveEditor.Tools.Common/     Code the tools share: archive decryption, hashing, parsing
└── TwdSaveEditor.Tools.<Name>/     One project per tool (ExtractKey, TtarchDecrypt, FinalValidation, ...)
```

NuGet package versions are managed centrally in `Directory.Packages.props`.

## Architecture

The editor is built from season plugins behind a small set of abstractions:

```
Web ──► UI ──► Season.Common ──► Core
 │                  ▲
 └──► Bootstrap ──► Season.S1 … Season.Michonne ──► Season.Base ──► Core.Binary ──► Core
```

- **`TwdSaveEditor.UI`** references only `Season.Common` and `Core`. It never names a concrete season or a binary reader; everything season-specific it shows comes from the registered `ISeasonHandler`s.
- **Season 2** also references `Season.S1`, because its save carries the Season 1 decisions under the same persistent keys.
- **Each season project** owns its season: display name, episodes, embedded choice and scene data, how a save is created, and how its choices are read and written.
- **`TwdSaveEditor.Bootstrap`** is the only place that knows the concrete seasons. `Web` calls `AddTwdSaveEditorServices()` and `AddTwdSaveEditorUI(...)`.

Each season implements `ISeasonHandler`:

```csharp
public interface ISeasonHandler
{
    string SeasonKey { get; }
    string Name { get; }
    string ShortName { get; }
    string FilePrefix { get; }
    IReadOnlyList<EpisodeInfo> Episodes { get; }
    IReadOnlyList<ChoiceDefinition> Choices { get; }
    IReadOnlyList<string> IncludedSeasonKeys { get; }
    IReadOnlyList<string> ImportsFromSeasonKeys { get; }
    string GetEpisodeId(int episode);
    IReadOnlyList<string> GetScenes(string episodeId);
    bool CanHandle(string fileName);
    SaveSlot CreateBlankSave(string fileName, string episodeId);
    IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    void PopulateChoices(SaveSlot slot, int episode);
}
```

Optional capabilities are separate interfaces a handler can also implement:

| Interface | Capability | Implemented by |
|-----------|------------|----------------|
| `ICompanionFileHandler` | State kept in files next to the bundle (autosave bundle, estore/epage EventLog) | S1, S2, S3, Michonne |
| `IResumePointHandler` | Restart a save from the beginning of an episode or from a chapter | S1, S2, S3, Michonne |
| `IInventoryHandler` | List and change the items the player character carries in the save a slot resumes from | S1, S2, S3, Michonne |
| `IPropertyNameProvider` | Names for the property hashes shown in the Properties tab | S1 |
| `IChoiceImporter` | Import all choices from a save of an earlier season | S2, S3 |
| `IChoicePresetProvider` | One-click presets that set several choices | S4 |

### Adding a season

1. Create a `TwdSaveEditor.Season.<Name>` project referencing `Season.Base` and `Season.Common`.
2. Add `Data/<key>.choices.json` and `Data/<key>.scenes.json` (embedded automatically).
3. Implement a handler under `Handlers/`, deriving from `PropChoicesSeasonHandler`, `StorySeasonHandler` or `SeasonHandlerBase` depending on how the season stores choices.
4. Register it in `TwdSaveEditor.Bootstrap`.

No changes to the UI are needed.

## Technical Details

### Binary Formats

- **MetaStream (MSV6)** — Telltale's container format with 3 sections (default, debug, async). A section whose size has the top bit set is TTCZ compressed: 64 KiB pages of raw deflate behind a page offset table. The debug section holds 4 bytes for every symbol in the default section
- **PropertySet** — Typed key-value store using CRC64 symbol hashes, version 2, grouped by type and ordered by hash. `DCArray<String>` values are a count followed by length-prefixed strings
- **Bundle** — Outer MetaStream whose default section is a table of 40-byte entries (offset, size, 16-byte name, name symbol, type symbol) and whose async section holds the inner MetaStream files. An autosave holds thousands of files; all but the first two are named by symbol only
- **EventLog** — `EventStorage` (page list, last event id, current page) and `EventStoragePage` files of `EventLoggerEvent` records; see Season 2 and Season 3 above.
- **Estore/Epage** — Paged EventLog storage (estore = index, epage = data pages)

### Choice Identification

- **S1**: Persistent keys from the game's `persistent.prop` (`Persistent - 101 - DougCarley Saved` = `carley`)
- **S2**: The same keys without the prefix for imported S1 decisions, string pairs (`"shot_kenny - true"`) for its own
- **S3**: Expressions over dialog node GUIDs from `persistent.prop` and `choice.prop`, evaluated against the EventLog (CRC64 of `{GUID}`)
- **S4**: GUIDs in `( {GUID} )` format (tab-separated in choicestats.pro)
- **Michonne**: The same as Season 3, from its own `persistent.prop` and `choice.prop`

## Tools

The `tools/` directory contains console tools used during development to decrypt and analyze game archives. These are **not required to run the editor** — all extracted data is already embedded in the application.

Each tool is its own project, `tools/TwdSaveEditor.Tools.<Name>`, run with `dotnet run --project`. The code they share lives in the `tools/TwdSaveEditor.Tools.Common` class library.

To use the tools yourself, you need:

1. The `TWD_ARCHIVES` environment variable pointing to the game's Archives directory
2. A `tools/key.txt` file containing the Blowfish encryption key (55 bytes, hex-encoded). The `ExtractKey` tool reads it from `WDC.exe` (offset `0xC3D7A0`) and checks that it decrypts an archive.

```bash
export TWD_ARCHIVES="/path/to/The Walking Dead The Telltale Definitive Series/Archives"
dotnet run --project tools/TwdSaveEditor.Tools.ExtractKey
dotnet run --project tools/TwdSaveEditor.Tools.TtarchDecrypt
```

| Tool | Purpose | Reads |
|------|---------|-------|
| `ExtractKey` | Extract the archive key from `WDC.exe` into `tools/key.txt` | `TWD_ARCHIVES` |
| `TtarchDecrypt` | Decrypt an archive and list what it contains | `TWD_ARCHIVES` |
| `ExtractArchive` | Extract files from archives into a directory, decrypting Lua scripts, or list them with `--list` | `TWD_ARCHIVES` |
| `DumpBundle` | Print every file and property of save bundles, `.prop`, `.dlog` and `.landb` files with resolved names, or write the raw sections with `--sections` | file arguments |
| `DumpDialog` | Write each `.dlog` file as a readable script (items, checkpoints, flag tests and assignments, scene loads) to an output directory, or to the console with `-` | file arguments |
| `Symbols` | Hash a string (`hash`) or look up the name of a symbol (`find`) | arguments |
| `ExtractSeason1Choices` | Read Season 1's persistent keys and values from the game and compare them with the editor's choice data | `TWD_ARCHIVES` |
| `ExtractChapters` | Read each Season 1 episode's developer chapter menu (scene script and flags per chapter) and the scene loads, checkpoints and decision tags in its dialogs, and its inventory items with the dialog rules that give and take them; writes everything to `tools/data/season1_chapters.json` and the editor's chapter list to `src/TwdSaveEditor.Season.S1/Data/s1.chapters.json` and item data to `s1.items.json` | `tools/data/lua`, `tools/data/extracted` (`.dlog` and `.scene` files) |
| `BuildCheckpoint` | Write a slot and the editor's chapter checkpoint for it with `chapter`, list chapter ids with `chapters`, build reduced or from-scratch checkpoints from a real autosave for experiments, or check with `--verify` that every `default.save` is rewritten identically | file arguments |
| `VerifyRoundTrip` | Read and rewrite every bundle, `.estore` and `.epage` file under the given paths and check the result is identical | file arguments |
| `EditSave` | Load a save the way the app does (or create one with `--new <episode>`), list its decisions, apply `key=value` changes, `--restart <episode>` or `--chapter <episode> <chapter id>`, change the inventory with `--carried`, `--give <item id>[:count]` and `--take <item id>`, and write the files (optionally as another slot number with `--out` and `--slot`) | file arguments |
| `ExtractDecisions <season>` | Build the season's decision list from `persistent.prop` and `choice.prop` into the season project's `Data` folder: node lists joined through the randomizer script for Season 2, node expressions and story keys (`<season>.decisions.json`) for Season 3 and Michonne. `<season>` is `2`, `3` or `m` | `tools/data/lua`, `tools/data/extracted` |
| `ExtractResumePoints <season>` | Read each episode's developer chapter menu and the chapter checkpoints in its dialogs and place every decision by the scene it is made in (`<season>.chapters.json`), for `2`, `3` or `m`; for Season 2 also list the imported Season 1 keys; read each episode's items (Season 2: `ui_item_*.prop`, names from `ui_episode.dlog` and `ui_episode_english.landb`; Season 3 and Michonne: `Inventory_InitItem` calls) and where scripts and dialog rules add or remove them (`s<N>.items.json`). A dialog is placed on the scene script that sets it with `Game_SetSceneDialog`, whose scene is named like it, that names it, or whose `.scene` file refers to it | `tools/data/lua`, `tools/data/extracted`, the season's decision file |
| `ExtractAllChoices` | Extract every season's choices into `tools/all_choices_summary.txt` | `TWD_ARCHIVES` |
| `ExtractNodeMappings` | Map choices to dialog node hashes in `tools/node_hash_mappings.txt` and `.json` | `TWD_ARCHIVES`, optionally `TWD_SAMPLE_SAVES` |
| `ExtractScenes` | List the scenes of each episode in `tools/data/episode_scenes.json` | `TWD_ARCHIVES` |
| `ValidateSaves` | Check the save formats against the test saves and the game archives | `TWD_ARCHIVES` |
| `FinalValidation` | Validate save formats, choice mappings and hashes end to end | `TWD_ARCHIVES` |
| `DecodeEstore` | Decode EventLog estore/epage files | `TWD_SAMPLE_SAVES` |
| `ValidateEditedSaves` | Compare the Season 1 saves in the save directory with a backup | `TWD_SAVES`, `TWD_BACKUP` |
| `CreateEndEpisodeEstore` | Write an estore holding an End Episode event into the save directory | `TWD_SAVES` |
| `AnalyzeSaves` | Print the MetaStream section sizes of the files passed to it | file arguments |
| `DumpMetadata` | Dump the metadata properties of the Season 1 test saves, read with the editor's own bundle reader | test saves |

`DumpBundle`, `Symbols` and the validation steps resolve hashes with the optional files in `tools/data` (not committed):

- `tools/data/names/*.txt` — one name per line (property keys, type names)
- `tools/data/names/classes.json` — class layouts, `data/versiondb/global.vdb.json` from [TelltaleToolKit](https://github.com/iMrShadow/TelltaleToolKit)
- `tools/data/lua/` — decompiled game scripts, whose string literals are used as names

The game's scripts are Lua 5.2 bytecode. `ExtractArchive` decrypts them and unluac decompiles them, here in a container:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.ExtractArchive -- "WDC_pc_ProjectSeason1_data*" tools/data/extracted "*.lua"
docker run --rm -u $(id -u):$(id -g) -v "$PWD/tools/data:/data" -v "/path/to/unluac.jar:/unluac.jar:ro" eclipse-temurin:21-jdk \
  sh -c 'cd /data/extracted && find . -name "*.lua" | while read f; do mkdir -p "/data/lua/$(dirname "$f")"; java -jar /unluac.jar "$f" > "/data/lua/$f"; done'
```

`TWD_SAVES` is the game's save directory. `TWD_SAMPLE_SAVES` is a directory of saved games to study, laid out as `S3/Episode 1`, `S3/Episode 5/The end` and `Michonne`. Every directory can also be passed as an argument instead:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.ExtractKey -- "/path/to/The Walking Dead The Telltale Definitive Series"
```

## License

MIT License. See [LICENSE](LICENSE) for details.

The Walking Dead is a trademark of Robert Kirkman, LLC. Telltale and the Telltale logo are trademarks of Telltale, Inc.
