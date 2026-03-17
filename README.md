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
- **Cross-season import** — import Season 1 choices into Season 2 saves
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
Documents\Telltale Games\The Walking Dead The Telltale Definitive Series
```

Click **Open Save Directory** in the app and navigate there. The app reads all `.bundle`, `.estore`, and `.epage` files automatically.

## Project Structure

```
src/
├── TwdSaveEditor.Core/           Core library (binary parsing, game data, accessors)
│   ├── Binary/                   MetaStream, bundle, estore/epage readers/writers
│   ├── GameData/                 Choice database, season handlers, accessors
│   │   ├── Seasons/              Per-season handler implementations (ISeasonHandler)
│   │   └── ChoiceData/           Choice definitions as JSON (embedded resources)
│   ├── Model/                    SaveSlot, PropertySet, EventLogEntry
│   └── Hashing/                  CRC64 (Telltale's ECMA-182 variant)
└── TwdSaveEditor.Web/            Blazor WASM frontend
    ├── Components/               Razor components (DecisionEditor, PropertyEditor, etc.)
    ├── Services/                 SaveEditorService, FileSystemService
    └── wwwroot/                  Static assets, CSS, JS interop

tests/
├── TwdSaveEditor.Core.Tests/     164 unit + integration tests
│   ├── Unit/                     Binary format, hashing, choice database, metadata
│   └── Integration/              Real save parsing, full create/edit/save cycles, game compatibility
├── TwdSaveEditor.Playwright/     22 E2E browser tests
└── TestData/                     Representative save files for all seasons

tools/                            Python scripts for game data extraction
```

## Architecture

The editor uses a **strategy pattern** with dependency injection. Each season implements `ISeasonHandler`:

```csharp
public interface ISeasonHandler
{
    string SeasonKey { get; }
    bool UsesEventLog { get; }
    bool UsesChoiceStats { get; }
    SaveSlot CreateBlankSave(string fileName, string episodeId);
    IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    void PopulateChoices(SaveSlot slot, int episode);
}
```

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

The `tools/` directory contains Python scripts used during development to decrypt and analyze game archives. These are **not required to run the editor** — all extracted data is already embedded in the application.

To use the tools yourself, you need:

1. The `TWD_ARCHIVES` environment variable pointing to the game's Archives directory
2. A `tools/key.txt` file containing the Blowfish encryption key (55 bytes, hex-encoded). The key can be extracted from `WDC.exe` at offset `0xC3D7A0`.

```bash
export TWD_ARCHIVES="/path/to/The Walking Dead The Telltale Definitive Series/Archives"
python tools/ttarch_decrypt.py
```

## License

MIT License. See [LICENSE](LICENSE) for details.

The Walking Dead is a trademark of Robert Kirkman, LLC. Telltale and the Telltale logo are trademarks of Telltale, Inc.
