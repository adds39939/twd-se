# How the engine loads a save

These notes come from decompiling `WDC.exe` (see [decompiling.md](decompiling.md)) and from putting cut-down saves in front of the game. They settled one question early on: the engine does not need the thousands of per-agent property sets a real autosave carries. What a scene needs is decided by the game's Lua scripts and dialog files, not by the engine.

## The load routine

- `LoadGame(bundle)` only stores the bundle handle in the save manager and sets a three-frame render delay.
- The main loop calls the manager's process routine every frame, then the script update.
- The process routine handles two cases with the same code: a pending script name ending in `.lua` (that is `ResetGame(script, resourceSets)`, which `SubProject_StartEpisode` uses to start an episode) and a pending bundle (a load). For a load it reads `default.save` from the bundle, enables every resource set named in `mEnabledDynamicSets`, copies `mAgentInfo` into the manager, and for each symbol in `mRuntimePropNames` clears the runtime property set of that name and deserialises the bundle file with the same symbol into it. Then it sets the loaded flag and makes `mLuaDoFile` the pending script.
- The script update rebuilds the Lua state, sets the Lua global `loaded` from that flag, and runs the pending script. Agent transforms from `mAgentInfo` are applied later as agents are created; entries for agents that never appear just stay pending.
- Missing pieces are skipped, not fatal: a runtime property name with no file in the bundle, a resource set that does not exist, an agent that is not in the scene. A bundle with no `default.save` makes the routine fail without clearing the pending handle, and the game appears to retry every frame after tearing the scene down.

So a load is `ResetGame` plus an overlay of runtime property sets. A checkpoint built from scratch only has to carry `metadata_save.prop`, `default.save` and the logic property sets: the game logic, the checkpoint set with `Checkpoint Dialog Item`, and the save/load set with `SaveLoad - Chapter ID`.

## What a scene does on load

Every scene script checks `SaveLoad_IsFromLoad()` (the `loaded` global). On a load it skips its entry setup, such as `Episode_SetLeeState`, and replays the checkpoint dialog item. If the saved `checkpoint dialog node` matches, the game does not save again; otherwise it writes a full checkpoint of its own straight away.

This is why a reduced checkpoint that loads straight into a scene loses things like Lee's appearance, and why the editor never does that. Season 1 instead runs the scene *before* the chapter and has its dialog jump into the chapter's scene; Seasons 2 to 4 and Michonne use the developer menu marker described below. Either way the target scene runs its own setup and the game writes its own checkpoint.

Season 4 goes further than the others: its `Game.lua` takes `loaded` at face value and runs only the dialog named in `SaveLoad - Checkpoint Dialog File` and `Node`, so a hand-made save for that season must name the scene's entry dialog. See [season-4.md](season-4.md).

## Developer chapter menus

Every episode script (`WDEpisode.lua` in Season 1, `Episode.lua` later) still contains the developers' chapter select. Each entry names a scene script and, for a point inside a scene, the logic flags to set first, sometimes items too. This is the source of every chapter the editor offers: `ExtractChapters` reads Season 1's, `ExtractResumePoints` the others'.

From Season 2 on, the menu marks a jump by setting `Script - Previous` of the `logic_script` agent (`logic_systems` in Season 4) to `DebugMenu`, and every scene script checks for that marker to give itself the flags and items it would normally inherit from the scenes before it. A checkpoint written by the editor reproduces exactly this. Two kinds of entry are dropped because they would not work in the released game: entries whose scene files are not in the episode's archive (two in Michonne, moved to Episode 2), and entries whose setup is only registered under `if IsDebugBuild() then Callback_OnLogicReady:Add(...)` (five in Season 3, several in Season 4).

Season 1 has no marker; its entries just set `Logic[...]` flags and call `LoadScript("env_x.lua")`, which is what the editor's checkpoint dialog item triggers.

## Things that cost time

- Automatic ordering of decisions against chapters was tried for Season 1 and dropped: the menus are not strictly chronological and scene scripts are revisited. The two in-episode decisions with mirror flags are a hand-set table.
- A resume point whose scene is missing from the archive hangs on an endless loading screen rather than failing.
- The game lists a slot with no save bundle as empty (Seasons 2 and Michonne) or a slot whose log has no imported block as empty (Season 3), so the editor always leaves at least one save or writes the block.
- `File.Exists` is case-sensitive on Linux; the archives are not consistent about case, so the tools compare names case-insensitively.
