# Architecture

The editor is a Blazor WebAssembly app. Everything runs in the browser; saves are read and written through the File System Access API, with upload and download as the fallback.

## Projects

```
src/
├── TwdSaveEditor.Core/             Models and hashing shared by everything
│   ├── Model/                      SaveSlot, PropertySet, property values, event log model
│   ├── Constants/                  Known metadata keys, event types, bundle file names
│   ├── Hashing/                    CRC64 (Telltale's ECMA-182 variant)
│   ├── Database/                   Property name lookup
│   └── Serialization/              ISaveBundleSerializer
├── TwdSaveEditor.Core.Binary/      Readers and writers for the binary formats
│   ├── Primitives/                 Binary reader/writer helpers
│   ├── MetaStream/                 MetaStream container
│   ├── PropertySets/               PropertySet and choices-container codecs
│   ├── Compression/                TTCZ page compression
│   ├── Bundles/                    Bundle reader/writer, slot factory, the serializer
│   ├── SaveGames/                  default.save codec
│   ├── EventLog/                   Estore/epage codec
│   └── Data/                       Layouts of the engine types found in property sets (embedded)
├── TwdSaveEditor.Season.Common/    Contracts a season implements
│   ├── Abstractions/               ISeasonHandler, ISeasonRegistry, IChoiceAccessor, capability interfaces
│   ├── Model/                      ChoiceDefinition, EpisodeInfo, ChoicePreset, CompanionFile, ResumeState, InventoryState
│   ├── Services/                   SeasonRegistry, BackupFileResolver
│   └── Extensions/                 CreateSave helpers
├── TwdSaveEditor.Season.Base/      Building blocks shared by the season projects
│   ├── Handlers/                   SeasonHandlerBase, StorySeasonHandler
│   ├── Checkpoints/                Bundle assembly and flag writing shared by every checkpoint builder
│   ├── DialogLog/                  Event log editing, companion files, node expressions, checkpoint naming
│   ├── Story/                      Decisions, resume points, checkpoints and items of Seasons 3, 4 and Michonne; Season 2 uses its chapter records
│   ├── Resources/                  Embedded season data loader
│   └── Extensions/                 DI registration of the shared services
├── TwdSaveEditor.Season.S1/        Season 1 and 400 Days
├── TwdSaveEditor.Season.S2/        Season 2
├── TwdSaveEditor.Season.S3/        A New Frontier
├── TwdSaveEditor.Season.S4/        The Final Season
├── TwdSaveEditor.Season.Michonne/  Michonne
│   ├── Handlers/                   The season's ISeasonHandler
│   ├── Data/                       Choice, scene, decision, chapter and item data as embedded JSON
│   └── Extensions/                 AddSeason<N>() registration
├── TwdSaveEditor.Bootstrap/        Composition root
├── TwdSaveEditor.UI/               Razor class library: pages, components, services, styles, images, JS
└── TwdSaveEditor.Web/              Blazor WASM host: Program.cs, index.html, manifest, service worker
```

Dependencies run one way:

```
Web ──► UI ──► Season.Common ──► Core
 │                  ▲
 └──► Bootstrap ──► Season.S1 … Season.Michonne ──► Season.Base ──► Core.Binary ──► Core
```

- `UI` references only `Season.Common` and `Core`. It never names a concrete season or a binary reader; everything season-specific it shows comes from the registered `ISeasonHandler`s, and it reads and writes bundles through `ISaveBundleSerializer`.
- `Season.S2` also references `Season.S1`, because its save carries the Season 1 decisions under the same persistent keys.
- `Bootstrap` is the only project that knows the concrete seasons. `Web` calls `AddTwdSaveEditorServices()` and `AddTwdSaveEditorUI(...)`.

## Dependency injection

Each season project registers its own services in `Extensions/ServiceCollectionExtensions` (`AddSeason1()`, `AddSeason2()`, …, `AddStorySeasonServices()` for the shared Story code). A handler takes its services through its constructor: Season 1's handler, for instance, takes `IS1ResumePoint`, `IS1Inventory`, `IS1SaveFactory`, `IS1CheckpointRefresher` and `ISaveBundleSerializer`. This is what lets the handler tests fake a dependency with FakeItEasy.

Static classes remain where they hold constants (`SlotMetadataKeys`, `S1SlotFiles`), extension methods, pure format code (`BundleReader`, `MetaStreamCodec`, `TelltaleHash`) or season data (`S1ChoiceCatalog`, `S3Story`).

## The season contract

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
    (string SeasonKey, int Episode) DecisionGroupOf(int episode);
    bool CanHandle(string fileName);
    SaveSlot CreateBlankSave(string fileName, string episodeId);
    IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    void PopulateChoices(SaveSlot slot, int episode);
}
```

Optional capabilities are separate interfaces a handler can also implement:

| Interface | Capability | Implemented by |
|-----------|------------|----------------|
| `ICompanionFileHandler` | State kept in files next to the bundle (autosave, checkpoints, estore/epage) | all |
| `IResumePointHandler` | Restart a save from the beginning of an episode or from a chapter | all |
| `IInventoryHandler` | Items in the save a slot resumes from, or the collectibles of the slot | all |
| `IPropertyNameProvider` | Names for the property hashes shown in the Properties tab | S1 |
| `IChoiceImporter` | Import the choices of a save of an earlier season | S2, S3, S4 |
| `IChoicePresetProvider` | Presets that set several choices at once; `RevealsEnding` hides a preset until asked for | all |

### Adding a season

1. Create `TwdSaveEditor.Season.<Name>` referencing `Season.Base` and `Season.Common`.
2. Add `Data/<key>.choices.json`; the `Data/` folder is embedded automatically, and the Story seasons also carry `<key>.decisions.json`, `<key>.chapters.json` and `<key>.items.json`.
3. Implement a handler under `Handlers/`, deriving from `StorySeasonHandler` for a season on the shared Story engine or from `SeasonHandlerBase` otherwise.
4. Add `Extensions/ServiceCollectionExtensions` with an `AddSeason<Name>()` that registers the handler and its services, and call it from `Bootstrap`.

The UI needs no change.

## UI

`Home.razor` keeps all four tab panels (Decisions, Resume Point, Inventory, Properties) mounted and hides the inactive ones, so unsaved edits survive switching tabs and the Inventory tab follows a resume point that has not been saved yet. `SaveEditorService` owns the loaded saves, the dirty state and writing; `SaveBackupService` copies the files `IBackupFileResolver` names before a write. The picked folder is kept in IndexedDB and opened again on the next visit when the browser still grants access. Without a folder (uploaded files, or browsers without the File System Access API) Save downloads a zip of the changed files instead of writing them, and Discard Changes reloads a save from the files it was last uploaded or downloaded as. Every edit calls `SaveEditorService.MarkModified`, which advances the save's `Revision`; the tabs read a save again only when the selected save or its revision changes, so toasts do not re-evaluate every decision. Every outcome is reported as a toast; errors stay until they are closed. Component styles live in each component's `.razor.css`; only truly global rules are in `wwwroot/css/app.css`. Sizes are in `rem` against a 14px design base that `--ui-scale` in `app.css` multiplies (1.2 by default, so the editor reads like a 120% browser zoom at 100%); only hairline borders, shadows and the scrollbar stay in `px`. The icon components take their `Size` in design pixels and render it in `rem`.

Below 900px wide the editor becomes a single column and the page scrolls as a whole. The save list and the editor take turns: picking or creating a save opens the editor, and its Saves button goes back to the list. The tab bar and Save button stay pinned to the top, and the footer to the bottom. Below 600px the editors stack their fields and the Properties table shows each property as a card. Below 380px the footer drops its link icons. Breakpoints are in `px`, because `rem` in a media query ignores `--ui-scale`.

The version in the footer is the informational version of the `Web` assembly: `dev` in a local build, the tag in a release, which the release workflow passes as `-p:InformationalVersion`.

`dotnet publish` prerenders the start page into `index.html` with `BlazorWasmPreRendering.Build`, so the editor shows straight away instead of a loading spinner while the WebAssembly runtime downloads. The prerenderer calls the static `ConfigureServices` function in `Program.cs`, so every service registration belongs there. Until the runtime is up the page cannot respond, so components that need it check `RendererInfo.IsInteractive`: the Open Save Directory button reads Loading... and `app-ready` is only set once the app is interactive. `dotnet run` does not prerender and still shows the spinner.

The published site is plain static files for GitHub Pages. `InvariantGlobalization` leaves out the ICU culture data, since every date and number the editor reads or writes already uses the invariant culture. The `.br` and `.gz` copies the publish writes next to each file go unused on GitHub Pages, which compresses responses itself, but stay for hosts that serve precompressed files; the service worker caches only the originals. `fileSystem.js` gets a fingerprinted file name, which the import map in `index.html` points to, so a new release never runs against a cached copy of the old one; the SDK only does this for ES modules in a standalone app, so `app.css` keeps its plain name. The release workflow installs the `wasm-tools` workload, which relinks the WebAssembly runtime to about half its size.

`service-worker.published.js` replaces the empty development service worker on publish. It caches every file the build lists in `service-worker-assets.js`, checked against its hash, and answers from that cache, so the installed app opens without a connection. Each build gets its own cache; a new release's worker waits until every tab of the old one is closed, so an open tab never loses a file it still needs.

## Tests

```
tests/
├── TwdSaveEditor.Core.Tests/            Hashing, property name database
├── TwdSaveEditor.Core.Binary.Tests/     MetaStream, property sets, bundles, event log files
├── TwdSaveEditor.Season.Base.Tests/     Choice accessor, embedded season data, node expressions
├── TwdSaveEditor.Season.Common.Tests/   Registry, handlers, created saves, backups, cascade
├── TwdSaveEditor.Season.S1.Tests/       One project per season, mostly against the real saves in TestData
├── TwdSaveEditor.Season.S2.Tests/
├── TwdSaveEditor.Season.S3.Tests/
├── TwdSaveEditor.Season.S4.Tests/
├── TwdSaveEditor.Season.Michonne.Tests/
├── TwdSaveEditor.UI.Tests/              UI services with faked dependencies
├── TwdSaveEditor.Tests.Common/          TestDataHelper, TestSeasons (the real DI container), real-save loaders
├── TwdSaveEditor.Playwright/            Browser tests against a running dev server with an in-memory save directory
└── TestData/                            One or two real saves per season
```

`tests/Directory.Build.props` holds the test packages (xunit, FakeItEasy, coverlet) once, so each test project is only its project references. `TestSeasons.Registry` is built from `AddTwdSaveEditorServices()`, the same wiring the app uses.

The Playwright fixture publishes `TwdSaveEditor.Web` in Release into the test output, serves it from an in-process server on a free port (`Support/PublishedSite.cs`), and replaces `window.showDirectoryPicker` with an in-memory directory (`Support/FakeSaveDirectory.cs`), so the tests can open real saves from `TestData`, edit them and read the written bytes back without touching disk.

The Test workflow runs on every push and pull request to `main`, and a newer push to a pull request cancels the run before it. It installs `wasm-tools` like the Release workflow, so the Playwright tests run against the build that gets deployed, caches NuGet packages, and runs every test project in one `dotnet test`. The `main` ruleset requires its `test` check, so it has no path filters: a pull request that only changes docs still needs the check to pass. The Release workflow calls it on the tagged commit first and only publishes when every test passes.

## Build rules

`Directory.Build.props` treats warnings as errors and turns on code-style enforcement, with `.editorconfig` requiring braces on every `if`/`for`/`foreach` body and no unused usings. Package versions are central in `Directory.Packages.props`. There are no comments in the source; the docs are here instead.
