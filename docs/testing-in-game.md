# Checking saves against the game

Unit tests prove the files round-trip and match the structure of real saves. Only the game can prove that a save loads and plays, so every season's resume points, decisions and items were checked that way before being called done. This is the routine that was used.

## Build the saves

`EditSave` writes saves through the same code as the app. Put each case in its own slot number so several can be checked in one sitting:

```bash
mkdir -p /tmp/s1-test
dotnet run --project tools/TwdSaveEditor.Tools.EditSave -- tests/TestData/S1 wd1_saveslot2.bundle --out /tmp/s1-test --slot 1 --chapter 5 OnJewelryStore
dotnet run --project tools/TwdSaveEditor.Tools.EditSave -- tests/TestData/S1 wd1_saveslot2.bundle --out /tmp/s1-test --slot 2 --chapter 5 OnJewelryStore "Cut Off Arm=true"
dotnet run --project tools/TwdSaveEditor.Tools.EditSave -- tests/TestData/S1 wd1_saveslot2.bundle --out /tmp/s1-test --slot 3 --new 2
```

For a season with an event log, remember the slot's companions (`.estore`, `.epage`, checkpoints) are part of the save; `EditSave` writes them all.

## Place them

1. Make sure the game is not running; it rewrites the save directory on exit.
2. Back up what is in `Documents\Telltale Games\The Walking Dead Definitive` (under Proton: `steamapps/compatdata/1449690/pfx/drive_c/users/steamuser/Documents/Telltale Games/The Walking Dead Definitive`).
3. Copy the test files in, and write a `SHA256SUMS` next to them so you can tell afterwards which files the game rewrote.
4. Start the game, open the season, and load each slot.

## What to look for

- **The slot list.** A slot the game considers empty (no save bundle in Seasons 2 and Michonne, no imported block in Season 3) shows as empty or offers a new game. A slot restarted from an episode's beginning is listed as a new game until the first checkpoint; select it, pick the episode and press Play.
- **Loading.** A chapter save should show the scene before the chapter for a moment (Season 1) or open the chapter scene directly (later seasons), then play. An endless loading screen means the scene is not in the episode's archive, or (Season 4) the save has no entry dialog. A black screen that never moves meant, in Season 4, that the scene was waiting for a checkpoint dialog that was never named.
- **The game's own checkpoint.** Play to the next checkpoint and let the game save. Compare the directory with `SHA256SUMS`: the game should have replaced the editor's small save with a full one and left the slot consistent. A game checkpoint keeps the editor's chapter id if it has none of its own.
- **Decisions.** The recap ("Previously on…") and the end-of-episode stats show whether the log edits were read. For Season 1, Lee's arm and the weapon are quick visual checks of the mirror flags.
- **Items.** Open the inventory in the first scene; items added with "Add items picked up earlier" should be there and usable.

## Time per check

A single slot load and the first checkpoint takes about half a minute. A full round for one season (new saves at each episode, a restart, two or three chapters, an item change) is around fifteen minutes of game time, which was the usual size of one test round before fixing whatever it showed and going again.
