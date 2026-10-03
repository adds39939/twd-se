# Getting at the game's data

Everything the editor knows about decisions, chapters and items was read from the game's own files. Nothing here needs to be installed on the machine beyond the .NET SDK and Docker; the Java and Ghidra steps run in containers.

## 1. The archive key

The `.ttarch2` archives are Blowfish-encrypted. `ExtractKey` pulls the key out of `WDC.exe` and writes `tools/key.txt`:

```bash
export TWD_ARCHIVES="/path/to/The Walking Dead The Telltale Definitive Series/Archives"
dotnet run --project tools/TwdSaveEditor.Tools.ExtractKey
```

## 2. Extracting files

`ExtractArchive` takes an archive name pattern, an output directory and a file pattern. Lua scripts come out decrypted but still as bytecode.

```bash
dotnet run --project tools/TwdSaveEditor.Tools.ExtractArchive -- "WDC_pc_ProjectSeason1_data*" tools/data/extracted "*.lua"
dotnet run --project tools/TwdSaveEditor.Tools.ExtractArchive -- "WDC_pc_ProjectSeason1_data*" tools/data/extracted "*.prop"
dotnet run --project tools/TwdSaveEditor.Tools.ExtractArchive -- "WDC_pc_ProjectSeason1_data*" tools/data/extracted "*.dlog"
```

The archives you want per season are `WDC_pc_ProjectSeason1_*`, `WDC_pc_ProjectSeason2_*`, `WDC_pc_ProjectSeason3_*`, `WDC_pc_ProjectSeason4_*` and `WDC_pc_ProjectMichonne_*`, plus the shared `WDC_pc_WalkingDead*` ones for `persistent.prop` and the UI. `--list` prints what an archive holds without extracting it.

## 3. Decompiling the Lua

The scripts are Lua 5.2 bytecode. [unluac](https://sourceforge.net/projects/unluac/) decompiles them; download `unluac.jar` and run it with a JDK image, so Java never has to be installed:

```bash
docker run --rm -u $(id -u):$(id -g) \
  -v "$PWD/tools/data:/data" \
  -v "/path/to/unluac.jar:/unluac.jar:ro" \
  eclipse-temurin:21-jdk \
  sh -c 'cd /data/extracted && find . -name "*.lua" | while read f; do
           mkdir -p "/data/lua/$(dirname "$f")"
           java -jar /unluac.jar "$f" > "/data/lua/$f"
         done'
```

The result lands in `tools/data/lua/`, mirroring the extracted layout. A few scripts fail to decompile (unluac prints an exception and the output file is empty); none of the ones the tools read are affected.

What to look at first in a season:

| File | Holds |
|------|-------|
| `WDEpisode.lua` (S1) / `Episode.lua` (S2 onwards) | The developer chapter menu, the item registrations |
| `PreviouslyOn.lua` | How an episode starts and which items it hands out |
| `ChoiceRandomizer.lua` (S2) | One dialog node per option of every decision |
| `env_*.lua` / scene scripts | The `DebugMenu` marker checks, `SaveLoad_IsFromLoad()` paths |
| `Game.lua` (S4) | The loaded-scene behaviour described in [season-4.md](season-4.md) |
| `Collectible.lua` (S4) | Collectible names per episode |

## 4. Reading the data files

`DumpBundle` prints any `.prop`, `.dlog`, `.landb` or save bundle with names resolved from `tools/data/names`. `DumpDialog` turns a `.dlog` into something readable:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.DumpBundle -- tools/data/extracted/persistent.prop
dotnet run --project tools/TwdSaveEditor.Tools.DumpDialog -- tools/data/extracted/env_jewelryStore.dlog -
```

Names come from three places: the `.txt` lists in `tools/data/names` (property keys, type names), `classes.json` from TelltaleToolKit, and the string literals of the decompiled scripts. Anything still unresolved is printed as its hash; `Symbols hash <text>` is the quick way to test a guess.

## 5. Decompiling the engine (optional)

This was only needed to understand the load routine ([engine.md](engine.md)) and how runtime property sets are named; the season work does not depend on it.

1. The Steam build is wrapped in Steam DRM. [Steamless](https://github.com/atom0s/Steamless) removes the wrapper from your own copy; its CLI runs under the `mono:latest` image with `MONO_PATH` pointing at its `Plugins` folder, and writes `WDC.unpacked.exe`.
2. Analyse that binary with Ghidra in the `blacktop/ghidra` image (`analyzeHeadless <project dir> <name> -import WDC.unpacked.exe`, then `-process ... -noanalysis` for later script runs). Ghidra's RTTI analyser does not run on this binary, so there are no vftable symbols.
3. The Lua API is the easiest way in: the engine registers about 1,500 script-callable functions by name. A Ghidra script that walks those registrations and renames each target `Lua_<Name>` gives you `Lua_LoadGame`, `Lua_ResetGame` and friends to start from.

Keep the engine material under `tools/data/engine/`, which is gitignored like the rest of `tools/data`.
