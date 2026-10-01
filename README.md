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
- **Resume point editing** — set episode, chapter, and completion status
- **Cross-season cascade** — optionally propagate choice changes through the chain (S1→S2→S3→S4), matching the game's native import flow
- **S4 presets** — quick-apply "Save Louis", "Save Violet", or "Trust AJ" choice paths
- **New save creation** — create blank saves for any season with pre-populated choices
- **File System Access API** — read/write directly to your save directory (Chromium-based browsers)
- **Upload fallback** — file upload + download for browsers without directory access

## How It Works

Each season stores choices differently. The editor handles all five formats natively:

| Season | Format | Storage |
|--------|--------|---------|
| Season 1 | `choices.prop` | ChoicesContainer PropertySet in bundle |
| Season 2 | `season1.prop` | Imported S1 choices in bundle |
| Season 3 | EventLog | 42-byte records in estore/epage files |
| Season 4 | `choicestats.pro` | Tab-separated GUIDs in bundle |
| Michonne | EventLog | 42-byte records with braced GUID hashes |

Choice definitions (134 total) are sourced from the game's own `choice.prop` data files.

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

Click **Open Save Directory** in the app and navigate there. The app reads all `.bundle`, `.estore`, and `.epage` files automatically.

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
│   ├── Bundles/                    Bundle reader/writer, metadata patcher, blank save factory
│   └── EventLog/                   Estore/epage reader, writer, creator
├── TwdSaveEditor.Season.Common/    Contracts a season implements
│   ├── Abstractions/               ISeasonHandler, ISeasonRegistry, IChoiceAccessor, capability interfaces
│   ├── Model/                      ChoiceDefinition, EpisodeInfo, ChoicePreset, CompanionFile
│   ├── Services/                   SeasonRegistry, BackupFileResolver
│   └── Extensions/                 CreateSave / GetScenes helpers
├── TwdSaveEditor.Season.Base/      Building blocks shared by season implementations
│   ├── Handlers/                   SeasonHandlerBase, PropChoicesSeasonHandler, EventLogSeasonHandler
│   ├── Accessors/                  SaveAccessor (choices.prop), EventLogAccessor (estore/epage)
│   ├── EventLog/                   ChoiceNodeMap, EventLog file naming
│   ├── Resources/                  Embedded choice/scene data loader
│   └── Services/                   SaveManager (disk-based)
├── TwdSaveEditor.Season.S1/        Season 1 and 400 Days
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
| `ICompanionFileHandler` | State kept in files next to the bundle (estore/epage EventLog) | S3, Michonne |
| `IChoiceImporter` | Import all choices from a save of an earlier season | S2 |
| `IChoicePresetProvider` | One-click presets that set several choices | S4 |

### Adding a season

1. Create a `TwdSaveEditor.Season.<Name>` project referencing `Season.Base` and `Season.Common`.
2. Add `Data/<key>.choices.json` and `Data/<key>.scenes.json` (embedded automatically).
3. Implement a handler under `Handlers/`, deriving from `PropChoicesSeasonHandler`, `EventLogSeasonHandler` or `SeasonHandlerBase` depending on how the season stores choices.
4. Register it in `TwdSaveEditor.Bootstrap`.

No changes to the UI are needed.

## Technical Details

### Binary Formats

- **MetaStream (MSV6)** — Telltale's container format with 3 sections (default, debug, async), optional TTCZ/zlib compression
- **PropertySet** — Typed key-value store using CRC64 symbol hashes, version 2 with type groups
- **Bundle** — Outer MetaStream containing a file table + inner MetaStream files
- **EventLog** — 42-byte fixed-size records with event type hash, dialog node hash, sequence index
- **Estore/Epage** — Paged EventLog storage (estore = index, epage = data pages)

### Choice Identification

- **S1/S2**: String key-value pairs (`"dougcarley_saved - carley"`)
- **S3**: Decimal CRC64 hashes of dialog node IDs (matched against EventLog)
- **S4**: GUIDs in `( {GUID} )` format (tab-separated in choicestats.pro)
- **Michonne**: CRC64 of braced GUID strings (`CRC64("{GUID}")`)

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

`TWD_SAVES` is the game's save directory. `TWD_SAMPLE_SAVES` is a directory of saved games to study, laid out as `S3/Episode 1`, `S3/Episode 5/The end` and `Michonne`. Every directory can also be passed as an argument instead:

```bash
dotnet run --project tools/TwdSaveEditor.Tools.ExtractKey -- "/path/to/The Walking Dead The Telltale Definitive Series"
```

## License

MIT License. See [LICENSE](LICENSE) for details.

The Walking Dead is a trademark of Robert Kirkman, LLC. Telltale and the Telltale logo are trademarks of Telltale, Inc.
