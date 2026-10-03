# Tools

`tools/` holds the console programs used to pull data out of the game and to check the editor against it. None of them are needed to run the editor; everything they produce is embedded in the season projects. Each tool is its own project, `tools/TwdSaveEditor.Tools.<Name>`, run with `dotnet run --project`, and the code they share is in `tools/TwdSaveEditor.Tools.Common`.

## Setup

Most tools need two things:

1. `TWD_ARCHIVES` pointing at the game's `Archives` directory.
2. `tools/key.txt`, the Blowfish key the archives are encrypted with (55 bytes, hex). `ExtractKey` reads it out of `WDC.exe` and checks that it decrypts an archive.

```bash
export TWD_ARCHIVES="/path/to/The Walking Dead The Telltale Definitive Series/Archives"
dotnet run --project tools/TwdSaveEditor.Tools.ExtractKey
dotnet run --project tools/TwdSaveEditor.Tools.TtarchDecrypt
```

The extraction tools then work from `tools/data/` (gitignored):

- `tools/data/extracted/` — files pulled from the archives (`.prop`, `.dlog`, `.scene`, `.landb`, Lua bytecode)
- `tools/data/lua/` — the decompiled scripts, see [decompiling.md](decompiling.md)
- `tools/data/names/*.txt` — one name per line, used to resolve hashes; `names/classes.json` is the class layout file from [TelltaleToolKit](https://github.com/iMrShadow/TelltaleToolKit) (`data/versiondb/global.vdb.json`)

`TWD_SAVES` is the game's save directory. `TWD_SAMPLE_SAVES` is a directory of saved games to study, laid out as `S3/Episode 1`, `S3/Episode 5/The end`, `Michonne` and so on. Every directory can also be passed as an argument instead.

## The tools

| Tool | Purpose | Reads |
|------|---------|-------|
| `ExtractKey` | Extract the archive key from `WDC.exe` into `tools/key.txt` | `TWD_ARCHIVES` |
| `TtarchDecrypt` | Decrypt an archive and list what it contains | `TWD_ARCHIVES` |
| `ExtractArchive` | Extract files matching a pattern from archives into a directory, decrypting Lua scripts, or list them with `--list` | `TWD_ARCHIVES` |
| `DumpBundle` | Print every file and property of save bundles, `.prop`, `.dlog` and `.landb` files with resolved names, or write the raw sections with `--sections` | file arguments |
| `DumpDialog` | Write each `.dlog` as a readable script (items, checkpoints, flag tests and assignments, scene loads) to a directory, or to the console with `-` | file arguments |
| `Symbols` | Hash a string (`hash`) or look up the name of a symbol (`find`) | arguments |
| `ExtractSeason1Choices` | Read Season 1's persistent keys and values from the game and compare them with the editor's choice data | `TWD_ARCHIVES` |
| `ExtractChapters` | Read each Season 1 episode's developer chapter menu, the scene loads, checkpoints and decision tags in its dialogs, and its items with the rules that give and take them; writes `tools/data/season1_chapters.json`, `Season.S1/Data/s1.chapters.json` and `s1.items.json` | `tools/data/lua`, `tools/data/extracted` |
| `ExtractDecisions <season>` | Build a season's decision list from `persistent.prop` and `choice.prop` into the season's `Data` folder: node lists joined through the randomizer script for Season 2, node expressions and story keys for 3, 4 and Michonne. `<season>` is `2`, `3`, `4` or `m` | `tools/data/lua`, `tools/data/extracted` |
| `ExtractResumePoints <season>` | Read each episode's developer chapter menu and the chapter checkpoints in its dialogs, place every decision by the scene it is made in (`<key>.chapters.json`), and read the episode's items and where scripts and dialog rules add or remove them (`<key>.items.json`); for Season 4 also the scene entry dialogs and the collectibles | `tools/data/lua`, `tools/data/extracted`, the season's decision file |
| `BuildCheckpoint` | Write a slot and the editor's chapter checkpoint for it (`chapter`), list chapter ids (`chapters`), build reduced or from-scratch checkpoints from a real autosave for experiments, or check with `--verify` that every `default.save` is rewritten identically | file arguments |
| `EditSave` | Load a save the way the app does (or create one with `--new <episode>`), list its decisions, apply `key=value` changes, `--restart <episode>` or `--chapter <episode> <chapter id>`, change the inventory with `--carried`, `--give <item id>[:count]` and `--take <item id>`, and write the files, optionally as another slot with `--out` and `--slot` | file arguments |
| `VerifyRoundTrip` | Read and rewrite every bundle, `.estore` and `.epage` under the given paths and check the result is identical | file arguments |
| `ValidateSaves` | Check the save formats against the test saves and the game archives | `TWD_ARCHIVES` |
| `FinalValidation` | Validate save formats, choice mappings and hashes end to end; three of its steps need `TWD_ARCHIVES` | `TWD_ARCHIVES` |
| `ValidateEditedSaves` | Compare the Season 1 saves in the save directory with a backup | `TWD_SAVES`, `TWD_BACKUP` |
| `ExtractAllChoices` | Every season's choices into `tools/all_choices_summary.txt` | `TWD_ARCHIVES` |
| `ExtractNodeMappings` | Map choices to dialog node hashes in `tools/node_hash_mappings.txt` and `.json` | `TWD_ARCHIVES`, optionally `TWD_SAMPLE_SAVES` |
| `ExtractScenes` | The scenes of each episode in `tools/data/episode_scenes.json` | `TWD_ARCHIVES` |
| `DecodeEstore` | Decode estore/epage files | `TWD_SAMPLE_SAVES` |
| `CreateEndEpisodeEstore` | Write an estore holding an End Episode event into the save directory | `TWD_SAVES` |
| `AnalyzeSaves` | Print the MetaStream section sizes of the files passed to it | file arguments |
| `DumpMetadata` | Dump the metadata of the Season 1 test saves with the editor's own bundle reader | test saves |

## Regenerating season data

The order matters, because the chapter extraction reads the decision file:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.ExtractDecisions -- 3
dotnet run --project tools/TwdSaveEditor.Tools.ExtractResumePoints -- 3
```

For Season 1 it is one tool, `ExtractChapters`. After regenerating, run the season's tests; several of them pin the number of decisions, chapters and items.

## Trying a change without the browser

`EditSave` goes through the same handlers as the app, so it is the quickest way to produce a save for an in-game check:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.EditSave -- tests/TestData/S1 wd1_saveslot2.bundle --out /tmp/out --chapter 5 OnJewelryStore "Cut Off Arm=true"
```

See [testing-in-game.md](testing-in-game.md) for what to do with the result.
