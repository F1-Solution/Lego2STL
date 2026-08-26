# The run is the document — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the run folder the thing the window is about — a persistent `run.json` manifest,
a history of runs, a four-item rail in place of four fake tabs — and fold in the four defects
the spec names.

**Architecture:** One projection, two sources. A run's presentable state is computed by exactly
one function, `RunDocument.From(RunManifest, RunLayout)`. A live run reaches it as
`RunOutcome → RunManifest.From(outcome) → written to disk and projected`; a reopened run as
`run.json → RunManifest → projected`. Live and reopened are therefore identical by
construction. `RunDocument` is an immutable Core record (so equality is testable in the Core
suite); `RunDocumentViewModel` wraps it with the live facet — progress, stage, log, cancel.

**Tech Stack:** .NET 10 (`net10.0` + `net10.0-windows10.0.19041.0`), Avalonia 12.1.1 + Fluent,
CommunityToolkit.Mvvm 8.4.2, System.Text.Json, xunit (v2 in `Lego2STL.Tests`, v3 in
`Lego2STL.UiTests`), FluentAssertions 7.2.0.

**Spec:** `docs/superpowers/specs/2026-08-26-run-document-window-design.md` — read it first.
Every "why" lives there; this plan is only the "how, in what order".

## Global Constraints

- Files stay under **800 lines**; nothing new above roughly **250**. Functions under **50**.
- `run.json` carries `"version": 1` from the first release.
- **No uppercase anywhere** in window text (Italian accented capitals are unreliable).
- Comments and CHANGELOG entries: **one sentence each**. Test comments may be longer.
- Every new `TextKey` gets an English **and** an Italian wording, with the same placeholders —
  `StringsTests` fails otherwise.
- Colour tokens are the exact hex values in the spec's token table. Do not re-derive them.
- `PipelineRunner` never writes outside the folder it was given: it writes the manifest, and
  **never** the index or the user profile.
- `RunIndex` has exactly two callers: `ConsoleRun` and the window's runner.
- The API key is written as the literal `<your key>` and nowhere else; one helper, two callers.
- Filtering option rows hides them with `IsVisible`; the list is never re-materialised.
- Build: `dotnet build Lego2STL.slnx`. Tests: `dotnet test Lego2STL.slnx`.
- Coverage target 80%.

---

## File structure

### Core — `src/Lego2STL.Core/Run/`

| File | Responsibility |
|---|---|
| `AppDataDirectory.cs` *(new)* | The `LEGO2STL_SETTINGS_DIR`-or-`ApplicationData/Lego2STL` rule, once. |
| `RunLayout.cs` *(modify)* | Gains `ManifestPath`, `LogPath`, `At(folder)`, `Plan(RunSettings)`. |
| `RunManifest.cs` *(new)* | The record, its JSON shape, `From(RunOutcome, …)`, read and write. |
| `RunDocument.cs` *(new)* | The one projection: `From(RunManifest, RunLayout)`, plus `WithoutManifest`. |
| `RunFolder.cs` *(new)* | Reads a folder back — manifest, parts list, which files are present. |
| `RunIndex.cs` *(new)* | The history: an ordered set of folder paths and nothing else. |
| `RunLogFile.cs` *(new)* | Open a log for writing, read one back. Shared by both front ends. |

### Core — elsewhere

| File | Change |
|---|---|
| `Pipeline/PipelineRunner.cs` | Manifest writes at its exits; `try`/`finally` for cancellation. Nothing else. |
| `Pipeline/RunSettings.cs` | `Problems()` speaks `TextKey`s, gains the OCR gate and the `WantsPlates` guard; `MaskedApiKey` helper. |
| `Text/TextKey.cs`, `Text/Strings.English.cs`, `Text/Strings.Italian.cs` | New keys, both languages. |

### The window — `src/Lego2STL.Gui/`

| File | Responsibility |
|---|---|
| `ViewLocator.cs` *(modify)* | Cache keyed by view-model instance; map `ViewModels` → `Views`. |
| `App.axaml` *(modify)* | Cool neutral as `ThemeDictionaries`, plus the `SystemAccentColor*` overrides. |
| `ViewModels/MainViewModel.cs` *(rewrite)* | Shell only: rail selection, language, the open run. |
| `ViewModels/RunsViewModel.cs` *(new)* | The list, newest first, read off the interface thread. |
| `ViewModels/SetupViewModel.cs` *(new)* | Input + the option list + `Start` + the planned log path. |
| `ViewModels/OptionRowsViewModel.cs` *(new)* | Search / changed-only / reset over the 18 run-shaping options. |
| `ViewModels/OptionRowViewModel.cs` *(new)* | One row, in five kinds: toggle, number, text, path, choice. |
| `ViewModels/RunDocumentViewModel.cs` *(new)* | The open run — live or from disk — and its catalogue. |
| `ViewModels/SettingsViewModel.cs` *(new)* | `--api-key`, `--log`, `--quiet`, `--lang`, forget history. |
| `ViewModels/CataloguePartViewModel.cs` *(modify)* | Takes recorded facts rather than a live mesh. |
| `Views/SetupView.axaml` *(new)*, `Views/OptionListView.axaml` *(new)*, `Views/RunDocumentView.axaml` *(new)*, `Views/RunsView.axaml` *(new)*, `Views/SettingsView.axaml` *(new)* | One view per screen. |
| `Views/CatalogueView.axaml` *(modify)* | Re-pointed at `RunDocumentViewModel`; opaque warning band. |
| `Views/MainWindow.axaml` *(rewrite)* | `SplitView` rail, `ContentControl` body, footer. |
| `Views/InputView.axaml`, `Views/OptionsView.axaml`, `Views/RunView.axaml` | **Deleted** in Task 17; their content moves into `SetupView` / `OptionListView` / `RunDocumentView`. |
| `Services/UserSettings.cs` *(modify)* | Uses `AppDataDirectory`. |

### Tests

| File | Covers |
|---|---|
| `tests/Lego2STL.Tests/Run/RunLayoutTests.cs` *(new)* | `At`, `Plan`, folder reuse, `ManifestPath`/`LogPath`. |
| `tests/Lego2STL.Tests/Run/RunManifestTests.cs` *(new)* | Round-trip, masking, defensive reads. |
| `tests/Lego2STL.Tests/Run/RunDocumentTests.cs` *(new)* | **The headline test**, plus `running` and no-manifest states. |
| `tests/Lego2STL.Tests/Run/RunFolderTests.cs` *(new)* | Fixture folders, including the no-manifest case. |
| `tests/Lego2STL.Tests/Run/RunIndexTests.cs` *(new)* | Record at start, de-duplicate, forget, honour the variable. |
| `tests/Lego2STL.Tests/Pipeline/PipelineManifestTests.cs` *(new)* | The five write points and their statuses. |
| `tests/Lego2STL.Tests/Pipeline/RunSettingsTests.cs` *(modify)* | `Problems()` in both languages, OCR gate, `--csv-only` fix. |
| `tests/Lego2STL.UiTests/OptionParityTests.cs` *(new)* | Enumerates the real CLI registration. |
| `tests/Lego2STL.UiTests/ViewLocatorTests.cs` *(new)* | Same view-model, same view instance. |
| `tests/Lego2STL.UiTests/RunDocumentViewTests.cs` *(new)* | Live and reopened render the same catalogue; log scroll survives. |
| `tests/Lego2STL.UiTests/OptionRowTests.cs` *(new)* | Search, changed-only, hidden count, reset. |
| `tests/Lego2STL.UiTests/WindowTests.cs`, `CatalogueTests.cs` *(modify)* | Re-pointed at the rail. |

---

## Task 1: The `ViewLocator` cache

Task one, before the rail lands. Today `Build` calls `Activator.CreateInstance` on every
request and maps `Lego2STL.Gui.ViewModels.FooViewModel` to
`Lego2STL.Gui.ViewModels.FooView` — a namespace that holds no views, so the locator has never
resolved anything. Both are fixed here: the namespace segment is mapped too, and each
view-model instance keeps its own view, which is what preserves the log's scroll position when
the rail switches away and back.

**Files:**
- Modify: `src/Lego2STL.Gui/ViewLocator.cs`
- Test: `tests/Lego2STL.UiTests/ViewLocatorTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `ViewLocator.Build(object?) : Control?` — same instance for the same view-model
  instance. Task 17's `MainWindow` relies on it.

- [ ] **Step 1: Write the failing test**

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// The locator has to hand back the same view for the same view model.
/// </summary>
/// <remarks>
/// A fresh view every time would cost the log its scroll position each time the rail moved
/// away and back, and would leave a screen's controls out of the logical tree while it is not
/// the one showing - which is exactly what the option parity test walks.
/// </remarks>
public sealed class ViewLocatorTests
{
    [AvaloniaFact]
    public void The_same_view_model_gets_the_same_view()
    {
        var locator = new ViewLocator();
        var model = new SettingsViewModel();

        var first = locator.Build(model);
        var second = locator.Build(model);

        first.Should().NotBeNull().And.BeOfType<Views.SettingsView>();
        second.Should().BeSameAs(first);
    }

    [AvaloniaFact]
    public void Two_view_models_get_two_views()
    {
        var locator = new ViewLocator();

        var first = locator.Build(new SettingsViewModel());
        var second = locator.Build(new SettingsViewModel());

        second.Should().NotBeSameAs(first);
    }
}
```

`SettingsViewModel` and `SettingsView` arrive in Task 16, so write the two tests above against
a view model that exists today and has no view — `RunOptionsViewModel`. Both claims still hold:
`Build` must return the same control for the same instance (here the fallback `TextBlock`), and
two instances must get two controls. Rename the first test's subject to `SettingsViewModel` and
assert `BeOfType<Views.SettingsView>()` in Task 16, once there is a view to find.

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.UiTests --nologo --filter ViewLocatorTests`
Expected: FAIL — two different `TextBlock` instances come back.
- [ ] **Step 3: Implement**

Replace the body of `ViewLocator` with a cache keyed by view-model **instance**, and map the
namespace segment as well as the type-name suffix — views live in `Lego2STL.Gui.Views`, which
today's mapping never reaches, so the locator has in fact never resolved anything.

```csharp
public class ViewLocator : IDataTemplate
{
    private readonly ConditionalWeakTable<object, Control> _built = [];

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        if (_built.TryGetValue(param, out var existing))
        {
            return existing;
        }

        var control = Create(param);
        _built.Add(param, control);
        return control;
    }

    public bool Match(object? data) => data is ViewModelBase;

    private static Control Create(object param)
    {
        // Views sit in their own namespace, so the segment is mapped as well as the suffix.
        var name = param.GetType().FullName!
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        return Type.GetType(name) is { } type
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "Not Found: " + name };
    }
}
```

`ConditionalWeakTable` rather than a `Dictionary`, so a view model the shell has let go does not
keep its view — and the view its subtree — alive.

- [ ] **Step 4: Run the tests**

Run: `dotnet test Lego2STL.slnx --nologo`
Expected: PASS, and the 26 existing UI tests still pass — nothing binds through the locator yet.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Gui/ViewLocator.cs tests/Lego2STL.UiTests/ViewLocatorTests.cs
git commit -m "fix: one view per view model, kept"
```

---

## Task 2: The parity test stops walking a hardcoded array

The net is re-hung before the wall comes down.
`Every_option_the_command_line_takes_is_named_on_the_options_screen` today walks a hardcoded
array of 21 names that omits `--quiet`, `--include-spares` and `--color-scheme`, so it passes
green while the window is missing an option. It is replaced by one that enumerates the real CLI
registration, which fails until `--quiet` reaches the window. At this point the window still has
one Options screen; Task 18 amends the same test to union Setup and Settings.

**Files:**
- Modify: `tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj` — add a `ProjectReference` to
  `src/Lego2STL.Cli/Lego2STL.Cli.csproj`
- Create: `src/Lego2STL.Cli/AssemblyInfo.cs` — `[assembly: InternalsVisibleTo("Lego2STL.UiTests")]`
- Create: `tests/Lego2STL.UiTests/OptionParityTests.cs`
- Modify: `tests/Lego2STL.UiTests/WindowTests.cs` — delete the old parity test
- Modify: `src/Lego2STL.Gui/ViewModels/RunOptionsViewModel.cs` — add `Quiet`
- Modify: `src/Lego2STL.Gui/Views/OptionsView.axaml` — a `--quiet` checkbox in Behaviour

**Interfaces produced:**
- `RunOptionsViewModel.Quiet : bool` (observable), carried into `ToSettings().Quiet`.
- `OptionParityTests.EveryFlag() : IReadOnlyList<string>` — the flags the CLI registers, reused
  by Task 18.
- `OptionParityTests.Texts(Visual) : IEnumerable<string>` — every piece of text a tree is
  showing. `WindowTests` keeps its own private copy; this one is `internal static` so the new
  tests share it.

`EveryFlag` builds `ExtractCommand.Create(Strings.English)` and `BuildCommand.Create(...)`,
selects `command.Options`, adds `CommonOptions.Language.Name`, keeps only names starting `--`,
and drops `--help`, `--version`, `--list-pages` and `--set` — the parser's own two, and the two
that *are* the input, chosen by radio button rather than typed. That leaves **23**: the 22
`PipelineOptions` registers (`--color-scheme` among them, from `extract`) plus `--lang`. Pin
that count in its own test, so a change to the CLI is visible here rather than silently
checking less.

- [ ] **Step 1:** Write `OptionParityTests` with three tests — the count guard,
  `Every_option_the_command_line_takes_is_named_in_the_window` (union of `Screen.Input` and
  `Screen.Options`), and nothing else yet.
- [ ] **Step 2:** Run: `dotnet test tests/Lego2STL.UiTests --nologo --filter OptionParityTests`.
  Expected: FAIL on `--quiet`.
- [ ] **Step 3:** Add `Quiet` to `RunOptionsViewModel` and a `--quiet` checkbox to
  `OptionsView.axaml`, hint `HelpOptQuiet`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx --nologo`. Expected: PASS.
- [ ] **Step 5:** Commit — `test: the window is checked against the options the command line really takes`.

---

## Task 3: `AppDataDirectory`, and what `RunLayout` now knows

Two answers both the manifest and the index need: where the profile lives, and what the run
folder will be *before* the run starts. `RunLayout.Plan` is also what fills `--log` in the
window, so it must agree with what `PipelineRunner` does — which it does by being the function
`PipelineRunner` itself calls.

**Files:**
- Create: `src/Lego2STL.Core/Run/AppDataDirectory.cs`
- Modify: `src/Lego2STL.Core/Run/RunLayout.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs` — take every layout from `RunLayout.Plan`
- Modify: `src/Lego2STL.Gui/Services/UserSettings.cs` — `FilePath` goes through `AppDataDirectory`
- Create: `tests/Lego2STL.Tests/Run/RunLayoutTests.cs`, `tests/Lego2STL.Tests/Run/AppDataDirectoryTests.cs`

**Interfaces produced:**

```csharp
namespace Lego2STL.Core.Run;

public static class AppDataDirectory
{
    public const string Variable = "LEGO2STL_SETTINGS_DIR";
    public static string Path { get; }               // the folder; not created
    public static string File(string name);
}

public sealed class RunLayout
{
    public string ManifestPath { get; }              // <root>/run.json
    public string LogPath { get; }                   // <root>/run.log
    public static RunLayout At(string folder);       // Name = the folder's leaf
    public static RunLayout? Plan(RunSettings settings);
    public static string SetFolderName(string setNumber);
}
```

`Plan` returns null when the input cannot name a folder, and swallows `ArgumentException`,
`NotSupportedException` and `PathTooLongException` — a half-typed path is the normal state of a
window being filled in. For a set number it is
`For(Path.Combine(OutputDirectory ?? CurrentDirectory, SetFolderName(set) + ".csv"), null)`;
otherwise `For(InputPath, OutputDirectory)`.

`UserSettings.DirectoryVariable` stays, as `= AppDataDirectory.Variable`, because
`tests/Lego2STL.UiTests/Isolation.cs` sets it. `PipelineRunner.RebrickableSetFolderName`
becomes a one-line call to `RunLayout.SetFolderName`.

- [ ] **Step 1:** Write `RunLayoutTests` — `At` names the folder's leaf; `Plan` is null for an
  empty input and for `""`; a document's plan sits beside it; a parts list inside a matching
  folder reuses that folder; a set number gives `set-42100-1` under `--output-dir`, and `42100`
  normalises to `42100-1`; `ManifestPath` and `LogPath` are inside `Root`; and **`Plan` equals
  what `For` produces for the same input**, which is the claim the window's shown log path rests
  on.
- [ ] **Step 2:** Run: `dotnet test tests/Lego2STL.Tests --filter RunLayoutTests`. Expected: fails to compile.
- [ ] **Step 3:** Write `AppDataDirectory` and the `RunLayout` additions; re-point
  `UserSettings.FilePath` and `PipelineRunner`'s three `RunLayout.For` calls.
- [ ] **Step 4:** Write `AppDataDirectoryTests` — the variable wins when set,
  `ApplicationData/Lego2STL` otherwise. Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: the run folder can be worked out before the run starts`.

---

## Task 4: `RunManifest`

`run.json` in the run folder, schema `"version": 1`. This is the file other tools may read, so
it is versioned from the first release and its shape is settled here.

**Files:**
- Create: `src/Lego2STL.Core/Run/RunManifest.cs`
- Modify: `src/Lego2STL.Core/Pipeline/RunSettings.cs` — the masking helper
- Create: `tests/Lego2STL.Tests/Run/RunManifestTests.cs`

**Interfaces produced:**

```csharp
namespace Lego2STL.Core.Run;

/// <summary>How a run ended, as the manifest records it.</summary>
public enum RunStatus { Running, Complete, NeedsDecision, Failed, Stopped }

/// <summary>Whether a folder's manifest could be used.</summary>
public enum ManifestState { Present, Missing, Newer }

public sealed record ManifestStage(RunStage Stage, int Completed, int Total);

public sealed record ManifestFailure(string Part, string Reason);

public sealed record ManifestPart(
    int Id, string Part, int ColorCode, string Color, string Rgb, int Quantity,
    string? Title, string? Size, bool? IsClosed, int? OpenEdgeCount, double? ThinnestSpanMm);

public sealed record ManifestSettings
{
    // One property per input-only RunSettings property; no computed ones, because
    // RunSettings.Bed throws on a bed size that does not parse and a serialiser would call it.
    public static ManifestSettings From(RunSettings settings);
    public RunSettings ToSettings();
}

public sealed record RunManifest
{
    public int Version { get; init; } = CurrentVersion;
    public const int CurrentVersion = 1;

    public RunStatus Status { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public ManifestSettings Settings { get; init; }
    public string CommandLine { get; init; }
    public ManifestStage? LastStage { get; init; }

    public int EntryCount { get; init; }
    public int TotalPieces { get; init; }
    public int DistinctPartCount { get; init; }
    public int ShapeCount { get; init; }
    public int ClosedShapeCount { get; init; }
    public int PlateCount { get; init; }

    public IReadOnlyList<string> Unread { get; init; } = [];
    public IReadOnlyList<ManifestFailure> Failed { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? Error { get; init; }
    public IReadOnlyList<ManifestPart> Parts { get; init; } = [];

    public static RunManifest Starting(RunSettings settings, DateTimeOffset startedAt);
    public static RunManifest Stopped(RunSettings settings, DateTimeOffset startedAt,
                                      DateTimeOffset finishedAt, RunProgress? lastStage);
    public static RunManifest From(RunOutcome outcome, DateTimeOffset startedAt,
                                  DateTimeOffset finishedAt, RunProgress? lastStage);

    public static Task WriteAsync(RunLayout layout, RunManifest manifest, CancellationToken ct = default);
    public static (RunManifest? Manifest, ManifestState State) Read(string manifestPath);
}
```

JSON options, once, in `RunManifest`:
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `WriteIndented = true`,
`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`, and one
`new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)` so every enum is a readable
token: `needs-decision`, `parts-list`, `brick-link`, `shapes-and-plates`, `building-shapes`.
`char Delimiter` serialises as a one-character string, which System.Text.Json already does.

`RunStatus` from a `RunOutcome`: `Complete → Complete`, `Unverified → NeedsDecision`,
`Failed → Failed`. `Stopped` only ever comes from cancellation.

The masking rule, in `RunSettings`, used by `ToCommandLine` **and** `ManifestSettings.From`:

```csharp
    /// <summary>What is written in place of the key, so a secret never travels with a command.</summary>
    public const string MaskedApiKey = "<your key>";

    public static string? MaskApiKey(string? apiKey) =>
        string.IsNullOrWhiteSpace(apiKey) ? null : MaskedApiKey;
```

`ManifestSettings.ToSettings()` maps the mask back to `null`: a placeholder is not a key, and
feeding it to the API would fail confusingly. A rerun from an old folder therefore takes the
key from the Settings screen, which is where it lives.

`Read` is defensive, following `UserSettings`' precedent: no file → `(null, Missing)`;
`JsonException`, `IOException`, `UnauthorizedAccessException`, or a null result → `(null, Missing)`,
because a corrupt file is treated as missing; `Version > CurrentVersion` → `(manifest, Newer)`,
so the window can say it was made by a newer build and show what parses. `WriteAsync` creates
the folder, writes through a temp file and an atomic replace, and swallows `IOException`,
`UnauthorizedAccessException` and `JsonException` — a manifest that will not write must never
interrupt a run.

- [ ] **Step 1:** Write `RunManifestTests`: round-trip through a temp folder keeps every field;
  a key becomes `<your key>` on disk and `null` on the way back; `Starting` has status `running`,
  no parts and no finish time; `From` maps the three `RunResult`s to the three statuses;
  `Read` of a missing file is `Missing`; of `{` is `Missing`; of a manifest with
  `"version": 99` is `Newer` and still carries its status; the status token on disk really is
  `needs-decision`; and `WriteAsync` into a read-only path does not throw.
- [ ] **Step 2:** Run: `dotnet test tests/Lego2STL.Tests --filter RunManifestTests`. Expected: fails to compile.
- [ ] **Step 3:** Write `RunManifest.cs` and the `RunSettings` masking helper; re-point
  `ToCommandLine` at `MaskedApiKey`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: a run keeps a record of itself in its own folder`.

---

## Task 5: `RunDocument` — the one projection

The central idea, and the place the headline test points at.

**Files:**
- Create: `src/Lego2STL.Core/Run/RunDocument.cs`
- Create: `tests/Lego2STL.Tests/Run/RunDocumentTests.cs`

**Interfaces produced:**

```csharp
namespace Lego2STL.Core.Run;

public sealed record RunDocumentPart(
    int Id, string PartNumber, int BrickLinkColorCode, string ColorName, Rgb24 Rgb, int Quantity,
    string? Title, string? Size, bool? IsClosed, int? OpenEdgeCount, double? ThinnestSpanMm)
{
    /// <summary>Below this a wall is thinner than a common 0.4 mm nozzle can lay down.</summary>
    public const double ThinnestPrintableMillimetres = 0.8;

    public bool HasOpenEdges => IsClosed == false;
    public bool HasThinFeatures => ThinnestSpanMm is { } span && span < ThinnestPrintableMillimetres;
    public bool ShapeWasMeasured => IsClosed is not null;
}

public sealed record RunDocument
{
    public required string Folder { get; init; }
    public required string Name { get; init; }
    public required RunStatus Status { get; init; }
    public bool ManifestKnown { get; init; }          // false for a folder written before manifests
    public bool FromNewerBuild { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public RunSettings? Settings { get; init; }
    public string CommandLine { get; init; } = string.Empty;
    public ManifestStage? LastStage { get; init; }

    public int EntryCount { get; init; }
    public int TotalPieces { get; init; }
    public int DistinctPartCount { get; init; }
    public int ShapeCount { get; init; }
    public int ClosedShapeCount { get; init; }
    public int PlateCount { get; init; }

    public IReadOnlyList<string> Unread { get; init; } = [];
    public IReadOnlyList<ManifestFailure> Failed { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? Error { get; init; }
    public IReadOnlyList<RunDocumentPart> Parts { get; init; } = [];

    public string PartsListPath { get; init; }
    public string StlDirectory { get; init; }
    public string PlateDirectory { get; init; }
    public string ReportPath { get; init; }
    public string LogPath { get; init; }

    /// <summary>How far the run actually got, which is not 1 for a run that stopped.</summary>
    public double Progress { get; }
    public bool NeedsDecision => Status == RunStatus.NeedsDecision;
    public bool IsRunning => Status == RunStatus.Running;

    public static RunDocument From(RunManifest manifest, RunLayout layout);
    public static RunDocument WithoutManifest(RunLayout layout, PartsList? partsList);
}
```

`Progress` is `Status switch { Complete => 1, Running or NeedsDecision or Stopped or Failed =>
LastStage is { } s ? new RunProgress(s.Stage, s.Completed, s.Total).Fraction : 0 }` — which is
defect 1's "the bar shows where it actually stopped rather than 100%". `Failed` with no stage is
0, as today.

`From` must handle a manifest whose status is `running`, with no parts, no counts and no
outcome. That is a live run at second one *and* a reopened row for a run killed mid-flight, so
it is a first-class case rather than an edge one.

`WithoutManifest` is for a folder written before manifests existed. It sets
`ManifestKnown = false` and leaves `Status = RunStatus.Stopped` — the truthful answer, because
nothing on disk says how that run ended — lists whatever the parts list holds with
`IsClosed`, `OpenEdgeCount` and `ThinnestSpanMm` all null, and leaves `CommandLine` empty. The
window reads `ManifestKnown == false` and shows the pre-manifest banner in place of a status
word, with **Run it again**. The two shape warnings are unrecoverable and it says so, rather
than guessing: an STL is a triangle soup with no shared vertices, so a re-welded open-edge count
depends on the welding tolerance and could legitimately disagree with what the original run
reported.

- [ ] **Step 1: write the headline test.**

```csharp
    /// <summary>
    /// A live outcome and the same run re-read from disk project to an equal document.
    /// </summary>
    /// <remarks>
    /// This one assertion defends the whole architecture. Live and reopened runs reach the
    /// screen through the same function over the same manifest, so they cannot disagree; if
    /// this ever fails, they have drifted and everything else on the run's page is suspect.
    /// </remarks>
    [Fact]
    public async Task A_live_run_and_the_same_run_reopened_are_the_same_document()
    {
        var folder = TempFolder();
        var layout = RunLayout.At(folder);
        var outcome = APretendRun(layout);

        var live = RunManifest.From(outcome, Started, Finished, LastStage);
        await RunManifest.WriteAsync(layout, live);

        var (reread, state) = RunManifest.Read(layout.ManifestPath);
        state.Should().Be(ManifestState.Present);

        RunDocument.From(reread!, layout)
            .Should().BeEquivalentTo(RunDocument.From(live, layout));
    }
```

`BeEquivalentTo` rather than `==` because a record's generated equality compares list members
by reference, which would make the test pass or fail on identity rather than on content.

Also test: a `running` manifest projects with no parts and a fraction under 1; a
`needs-decision` manifest keeps its `Unread` and `Failed`; `WithoutManifest` reports
`ManifestKnown == false` and still lists the parts from a parts list; every path property is
inside `Folder`.

- [ ] **Step 2:** Run: `dotnet test tests/Lego2STL.Tests --filter RunDocumentTests`. Expected: fails to compile.
- [ ] **Step 3:** Write `RunDocument.cs`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: one projection for a run, live or reopened`.

---

## Task 6: `RunFolder`

**Files:**
- Create: `src/Lego2STL.Core/Run/RunFolder.cs`, `src/Lego2STL.Core/Run/RunLogFile.cs`
- Create: `tests/Lego2STL.Tests/Run/RunFolderTests.cs`

**Interfaces produced:**

```csharp
public sealed record RunFolder(
    RunLayout Layout, RunManifest? Manifest, ManifestState State, PartsList? PartsList,
    bool HasPartsList, bool HasShapes, bool HasPlates, bool HasReport, bool HasLog)
{
    public bool Exists { get; }

    /// <summary>Reads a folder back. Synchronous; callers run it off the interface thread.</summary>
    public static RunFolder Read(string folder);

    /// <summary>The document this folder amounts to, manifest or no manifest.</summary>
    public RunDocument ToDocument();
}

public static class RunLogFile
{
    public static StreamWriter? Open(string? path);              // null path, null writer
    public static IReadOnlyList<string> Read(string path);       // empty when unreadable
}
```

`Read` uses `PartsListCsv.Read(File.ReadAllText(path))` — the synchronous overload exists, so
`RunFolder.Read` needs no async. It swallows `IOException`, `InvalidDataException`,
`FormatException` and `UnauthorizedAccessException` per parts-list read: an unreadable list
leaves `PartsList` null and `HasPartsList` true, which is honest.

`ToDocument()` is `State == Missing ? RunDocument.WithoutManifest(Layout, PartsList) :
RunDocument.From(Manifest!, Layout) with { FromNewerBuild = State == ManifestState.Newer }`.

`RunLogFile.Open` is `ConsoleRun.OpenLog` moved to Core so both front ends and the reopened-run
log reader share one definition. `ConsoleRun` is re-pointed at it in Task 8.

- [ ] **Step 1:** Write `RunFolderTests` against fixture folders built in a temp directory: a
  full folder (manifest + csv + stl + 3mf + report + log) reports everything present; a folder
  with only a parts list is `Missing` and still yields a document listing its parts; a folder
  with a corrupt `run.json` behaves as the no-manifest case; a folder that does not exist has
  `Exists == false`; `RunLogFile.Read` of a missing file is empty; `RunLogFile.Open(null)` is null.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write both files.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: a run folder can be read back`.

---

## Task 7: `RunIndex`

An ordered set of folder paths and nothing else, so the rule *the index is a convenience, the
folder is the truth* is literally true: there is no cached summary that could disagree with a
manifest.

**Files:**
- Create: `src/Lego2STL.Core/Run/RunIndex.cs`
- Create: `tests/Lego2STL.Tests/Run/RunIndexTests.cs`

**Interfaces produced:**

```csharp
public static class RunIndex
{
    public static string FilePath { get; }                       // AppDataDirectory.File("runs.json")
    public static IReadOnlyList<string> Read();                  // newest first; empty when unreadable
    public static void Record(RunLayout layout);                 // move to front, de-duplicate
    public static void Forget(string folder);
    public static void ForgetEverything();
}
```

JSON: `{ "version": 1, "runs": [ "<full path>", ... ] }`. Paths are compared with
`OrdinalIgnoreCase` and stored as `Path.GetFullPath`. Writes go through a temp file in the same
folder and `File.Move(temp, FilePath, overwrite: true)`, because the terminal and the window
can run at once; last-writer-wins loses nothing when only paths are stored. Every write
swallows `IOException`, `UnauthorizedAccessException` and `JsonException` — losing a history row
must never cost a run.

- [ ] **Step 1:** Write `RunIndexTests` with `AppDataDirectory.Variable` pointed at a temp
  folder per test: recording puts the newest first; recording the same folder twice leaves one
  row, moved to the front; `Forget` removes one and leaves the rest in order;
  `ForgetEverything` empties it; `Read` of a missing or corrupt file is empty; the file really
  lands under the variable's folder; two `Record` calls in a row do not throw when the file is
  replaced between them.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write `RunIndex.cs`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: the runs that have happened are remembered`.

---

## Task 8: The pipeline writes the manifest, and the terminal records the run

**Files:**
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs`
- Modify: `src/Lego2STL.Cli/ConsoleRun.cs`
- Create: `tests/Lego2STL.Tests/Pipeline/PipelineManifestTests.cs`

`RunAsync` becomes the one place a manifest is written, so the five points the spec names are
five *states* rather than five scattered calls — and a run cannot write two:

1. **Before the pipeline starts** — `RunManifest.Starting`, status `running`, after
   `layout.CreateDirectories()`. The row exists and the settings survive a crash of the window.
2. **`Unverified` from unread entries** and 3. **`Complete` from `--csv-only`** and
   4. **the shapes exit** — all three are `RunOutcome`s returned by `RunCoreAsync`, so one
   `RunManifest.From(outcome, …)` write after it returns covers them, with the status each
   deserves.
5. **The `Failure` wrapper** — the `catch` writes a `failed` manifest — **and cancellation**,
   which a `finally` covers: without it a cancelled run leaves a manifest reading `running`
   forever.

```csharp
    private RunProgress? _lastProgress;          // what "stopped while building shapes, 38 of 91" reads

    public async Task<RunOutcome> RunAsync(RunSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var problems = settings.Problems();
        if (problems.Count > 0)
        {
            // Nothing has started and no folder has been named, so there is nothing to record.
            return RunOutcome.Failure(settings, string.Join(" ", problems));
        }

        var layout = RunLayout.Plan(settings);
        var started = DateTimeOffset.UtcNow;
        var recorded = false;

        Report(RunStage.Starting);

        if (layout is not null)
        {
            layout.CreateDirectories();
            await Record(layout, RunManifest.Starting(settings, started), cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var outcome = await RunCoreAsync(settings, cancellationToken).ConfigureAwait(false);

            await Record(
                    outcome.Layout ?? layout,
                    RunManifest.From(outcome, started, DateTimeOffset.UtcNow, _lastProgress),
                    cancellationToken)
                .ConfigureAwait(false);

            recorded = true;
            return outcome;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or IOException
                                       or FormatException
                                       or OcrUnavailableException
                                       or InvalidDataException
                                       or PlatformNotSupportedException)
        {
            // These all carry a message written for whoever is reading it; anything else is a
            // fault in the tool and is left to travel with its stack trace.
            var failure = RunOutcome.Failure(settings, ex.Message) with { Layout = layout };

            await Record(
                    layout,
                    RunManifest.From(failure, started, DateTimeOffset.UtcNow, _lastProgress),
                    CancellationToken.None)
                .ConfigureAwait(false);

            recorded = true;
            return failure;
        }
        finally
        {
            if (!recorded)
            {
                // Cancelled, or a fault on its way out: either way the manifest must not be
                // left reading "running" for ever.
                await Record(
                        layout,
                        RunManifest.Stopped(settings, started, DateTimeOffset.UtcNow, _lastProgress),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>Writes the manifest, and says so in the log if it cannot.</summary>
    private async Task Record(RunLayout? layout, RunManifest manifest, CancellationToken cancellationToken)
    {
        if (layout is null)
        {
            return;
        }

        await RunManifest.WriteAsync(layout, manifest, cancellationToken).ConfigureAwait(false);
    }
```

`Report` stores `_lastProgress`. `RunOutcome.Failure` gains no new members; the layout is
attached with `with`.

`ConsoleRun.ExecuteAsync` gains two lines: `RunLayout.Plan(settings)` recorded in the index
**before** the run — so a run that crashes or is killed still has a row — and `RunLogFile.Open`
in place of its private `OpenLog`.

- [ ] **Step 1:** Write `PipelineManifestTests`. Every case runs the real `PipelineRunner`
  against a fixture parts list in a temp folder, so no network and no recogniser are needed:
  a `--csv-only` run leaves `complete`; a run whose parts all fail leaves `needs-decision` with
  the failures named; a run against a missing file returns before any folder is made and writes
  nothing; a run cancelled part-way leaves `stopped` with a `lastStage`; a manifest exists with
  status `running` while the run is still going (assert from inside the progress callback);
  and `PipelineRunner` writes no index file — `RunIndex.Read()` stays empty with
  `AppDataDirectory.Variable` pointed at a temp folder.
- [ ] **Step 2:** Run and watch the new cases fail.
- [ ] **Step 3:** Rewrite `RunAsync` and re-point `ConsoleRun`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS, 336 + new.
- [ ] **Step 5:** Commit — `feat: a run records itself from the moment it starts`.

---

## Task 9: `Problems()` gets keys and a gate

**Files:**
- Modify: `src/Lego2STL.Core/Pipeline/RunSettings.cs`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Modify: `tests/Lego2STL.Tests/Pipeline/RunSettingsTests.cs`

New keys, both languages: `ErrChooseDocument`, `ErrChoosePartsList`, `ErrTypeSetNumber`,
`ErrNoFileAt` (`{0}`), `ErrScaleNotPositive`, `ErrSpacingNegative`, `ErrNotABedSize` (`{0}`).
`ErrClearanceNegative` already exists and takes `{0}`, so `Problems()` passes the value.
`ErrOcrUnavailable` already exists in both languages and is used nowhere — the gate is its
first caller.

Three changes:
- Every string becomes `words[...]` / `words.Format(...)`, where `words = Strings.For(Language)`.
  No signature changes: `RunSettings` already carries `Language`.
- `Kind == InputKind.Document && !OcrEngines.IsAvailable` adds `ErrOcrUnavailable`, so the
  command line inherits the early refusal instead of dying at `RunStage.ReadingDocument`.
- The `PlateSize` check gains its missing `WantsPlates` guard, so a `--csv-only` run stops being
  blocked by a bed size no plate would use.

- [ ] **Step 1:** Add tests — a document run with no path says so in English and in Italian; a
  `--csv-only` run with `PlateSize = "nonsense"` has no problems; a `ShapesAndPlates` run with
  the same has one; the OCR gate refuses a document run on a build with no recogniser and does
  not refuse a parts-list run. Guard the OCR case on `OcrEngines.IsAvailable` so the assertion
  is written for whichever build is running rather than skipped.
- [ ] **Step 2:** Run and watch them fail.
- [ ] **Step 3:** Add the keys and rewrite `Problems()`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS — `StringsTests` covers the
  new keys in both languages.
- [ ] **Step 5:** Commit — `fix: what stops a run is said in the chosen language, and a document is refused early`.

---

## Task 10: The window's new words

One task for every new `TextKey` the four screens need, so no later task is held up waiting for
a phrase and no phrase is invented twice.

**Files:** `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`

**Added** (English / Italian):

| Key | English | Italian |
|---|---|---|
| `UiRailRuns` | Runs | Esecuzioni |
| `UiRailSetup` | Setup | Preparazione |
| `UiNewRun` | New run | Nuova esecuzione |
| `UiStatusRunning` | Running | In corso |
| `UiStatusComplete` | Complete | Completata |
| `UiStatusNeedsDecision` | Needs a decision | Richiede una scelta |
| `UiStatusFailed` | Failed | Non riuscita |
| `UiStatusStopped` | Stopped | Interrotta |
| `UiStoppedAt` | stopped while {0}, {1} of {2} | interrotta durante {0}, {1} di {2} |
| `UiRunsEmpty` | No runs yet. Set one up and it appears here. | Ancora nessuna esecuzione. Preparane una e comparirà qui. |
| `UiForget` | Forget | Dimentica |
| `UiForgetEverything` | Forget every run | Dimentica tutte le esecuzioni |
| `UiFolderMissing` | This folder is no longer there. | Questa cartella non c'è più. |
| `UiNoManifest` | This run was made before runs kept a record of themselves, so what its shapes measured is not known. Run it again to fill it in. | Questa esecuzione è precedente al registro delle esecuzioni, quindi le misure delle forme non sono note. Eseguila di nuovo per completarla. |
| `UiNewerManifest` | This run was recorded by a newer version of the program. What follows is the part this one understands. | Questa esecuzione è stata registrata da una versione più recente del programma. Segue la parte che questa versione capisce. |
| `UiRunItAgain` | Run it again | Esegui di nuovo |
| `UiOpenPartsList` | Open the parts list | Apri l'elenco pezzi |
| `UiContinueFromPartsList` | Continue from the parts list | Continua dall'elenco pezzi |
| `UiSearchOptions` | Search the options | Cerca fra le opzioni |
| `UiChangedOnly` | Only what I changed | Solo ciò che ho cambiato |
| `UiHiddenCount` | {0} hidden | {0} nascoste |
| `UiReset` | Reset | Ripristina |
| `UiQuietNote` | Nothing is being shown here because of --quiet. It is all in run.log. | Qui non compare nulla per via di --quiet. È tutto in run.log. |
| `UiWhen` | When | Quando |
| `UiRunFolder` | Run folder | Cartella dell'esecuzione |
| `UiStageReadingDocument` | Reading the document | Lettura del documento |
| `UiStageLookingUpSet` | Looking up the set | Ricerca del set |
| `UiStageReadingPartsList` | Reading the parts list | Lettura dell'elenco pezzi |
| `UiStageWritingPartsList` | Writing the parts list | Scrittura dell'elenco pezzi |
| `UiStageGatheringShapes` | Gathering the shapes | Raccolta delle forme |
| `UiStageBuildingShapes` | Building the shapes | Costruzione delle forme |
| `UiStageArrangingPlates` | Arranging the plates | Disposizione dei piani |
| `UiStageWritingReport` | Writing the report | Scrittura del resoconto |

**Removed:** `UiTabInput`, `UiTabOptions`, `UiTabCatalogue` — the four fake tabs go, and their
words with them. `UiTabRun` stays as the heading of a run's page. Delete their rows from both
tables in the same commit, or `StringsTests` fails.

- [ ] **Step 1:** Add the enum values and both tables; delete the three retired keys.
- [ ] **Step 2:** Run: `dotnet test tests/Lego2STL.Tests --filter StringsTests`. Expected: PASS
  — every key in both languages, same placeholders.
- [ ] **Step 3:** Run: `dotnet build Lego2STL.slnx`. Expected: FAIL where the retired keys were
  used, in `MainWindow.axaml`'s tabs and the three views' headings. Point those at the keys they
  will keep (`UiInputKind`, `UiGroupStages`, `UiTabRun`) so the build is green again; the markup
  itself is replaced in Tasks 13–17.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: the words the new screens need, in both languages`.

---

## Task 11: Cool neutral

Applied as `ResourceDictionary.ThemeDictionaries` keyed Light/Dark in `App.axaml`, not as
scattered literals, **together with** overrides of `SystemAccentColor` and
`SystemAccentColorLight1/2/3` and `Dark1/2/3` — that one move recolours every Fluent checkbox,
radio, spinner and focus ring at once, which is how the window stops borrowing the OS accent.

**Files:**
- Modify: `src/Lego2STL.Gui/App.axaml`
- Create: `tests/Lego2STL.UiTests/ThemeTests.cs`

Resource keys, defined in both variants — the exact hex from the spec's token table:
`AppWindowBackground`, `AppSurface`, `AppCardBorder`, `AppText`, `AppTextSecondary`,
`AppAccent`, `AppAccentHover`, `AppTextOnAccent`, `AppSuccess`, `AppWarningText`,
`AppWarningBand`, `AppWarningBorder`, `AppDanger`, `AppLogBackground`, `AppLogForeground`.
Each as a `SolidColorBrush`, plus a `Color` for the accent so the `SystemAccentColor*` family
can be set from it.

`RequestedThemeVariant="Default"` stays: the OS decides light or dark, and there is no in-app
theme switch.

- [ ] **Step 1:** Write `ThemeTests` — `Application.Current.TryFindResource` finds every one of
  the fifteen keys under `ThemeVariant.Light` and under `ThemeVariant.Dark`, and
  `SystemAccentColor` is the token's colour rather than the platform's. A loop over a `string[]`
  of the key names, so a key added to the table and not to a variant fails here.
- [ ] **Step 2:** Run and watch it fail.
- [ ] **Step 3:** Write the `ThemeDictionaries` block.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: the window has its own colours rather than the system's accent`.

---

## Task 12: The option list

The 18 run-shaping options as rows that can be searched, filtered to what changed, and reset one
at a time.

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/OptionRowViewModel.cs`,
  `src/Lego2STL.Gui/ViewModels/OptionRowsViewModel.cs`
- Create: `src/Lego2STL.Gui/Views/OptionListView.axaml` (+ `.axaml.cs`)
- Create: `tests/Lego2STL.UiTests/OptionRowTests.cs`

**Interfaces produced:**

```csharp
public abstract partial class OptionRowViewModel : ViewModelBase
{
    protected OptionRowViewModel(string flag, TextKey help);
    public string Flag { get; }
    public string Help { get; }                       // through Loc, so a language switch re-reads it
    public bool IsVisible { get; set; }               // filtering hides, never re-materialises
    public bool IsEnabled { get; }                    // --no-plates needs shapes; the plate three need plates
    public abstract bool IsChanged { get; }
    public abstract void Reset();
    [RelayCommand] private void ResetOne();
    internal Func<bool> Enabled { get; init; }
    public bool Matches(string? search);              // flag or help text
}

public sealed partial class ToggleOptionRow  : OptionRowViewModel { public bool Value { get; set; } }
public sealed partial class NumberOptionRow  : OptionRowViewModel { public double Value { get; set; }
                                                                   public double Minimum, Maximum, Increment;
                                                                   public string Format; }
public sealed partial class TextOptionRow    : OptionRowViewModel { public string? Value { get; set; }
                                                                   public string? Placeholder; }
public sealed partial class PathOptionRow    : TextOptionRow      { public bool WantsFolder; }
public sealed partial class ChoiceOptionRow  : OptionRowViewModel { public IReadOnlyList<string> Choices;
                                                                   public string? Value { get; set; } }

public sealed partial class OptionRowsViewModel : ViewModelBase
{
    public OptionRowsViewModel(RunOptionsViewModel options);
    public IReadOnlyList<OptionRowViewModel> Rows { get; }        // 18, in the spec's order
    public string? Search { get; set; }
    public bool ChangedOnly { get; set; }                        // default from the second run onward
    public int HiddenCount { get; }
    [RelayCommand] private void ResetAll();
}
```

Each row reads and writes the one `RunOptionsViewModel` through a getter and a setter delegate,
so there is still exactly one property per option and `ToSettings()` is untouched. `IsChanged`
compares against a `new RunOptionsViewModel()`'s value for the same option, which is what
`ToCommandLine` already computes, so changed-only costs nothing.

`ChangedOnly` **defaults to true from the second run onward**, and false on a first run when
there is nothing to compare against. `RunIndex.Read().Count > 0` is the whole of that test — no
new preference, because the history already records whether a run has ever happened. Its toggle
**carries the count of hidden rows**, so an option set three runs ago cannot sit invisible
without the window saying how many are hidden.

The 18, in five kinds: **toggles (8)** `--csv-only`, `--no-plates`, `--ascii`, `--keep-origin`,
`--repair`, `--no-seam-repair`, `--offline`, `--no-unofficial`; **numbers (4)** `--scale`,
`--clearance`, `--weld-tolerance`, `--plate-spacing`; **paths (3)** `--output-dir`,
`--ldraw-dir`, `--ldraw-cache`; **text (1)** `--plate-size`; **choices (2)** `--delimiter`,
`--printer`.

`OptionListView` is one `ItemsControl` over `Rows` with a `DataTemplate` per row type in
`ItemsControl.DataTemplates`, each row wrapped in a container whose `IsVisible` binds to the
row's. Not virtualised, so every row stays a logical descendant and the parity test can read a
hidden one. The search box, the changed-only toggle carrying `UiHiddenCount`, and a reset button
per row sit above and beside.

- [ ] **Step 1:** Write `OptionRowTests`: there are exactly 18 rows and their flags are exactly
  the spec's 18; a fresh list has nothing changed; setting `Clearance` marks `--clearance`
  changed and no other; `ChangedOnly` hides the rest and `HiddenCount` says how many; searching
  `weld` leaves `--weld-tolerance` visible; searching a word from a help text finds its row;
  `Reset` on a row puts the setting back and `IsChanged` goes false; a hidden row is still in
  `Rows`; `--no-plates` is disabled when `CsvOnly`; the three plate rows are disabled when
  `NoPlates`; and — the point of the whole indirection — writing a row's `Value` reaches
  `options.ToSettings()`.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write the two view models and `OptionListView`.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS. The old `OptionsView` is
  still what the window shows, so the 26 existing UI tests are untouched.
- [ ] **Step 5:** Commit — `feat: the options can be searched, filtered to what changed, and put back`.

---

## Task 13: Setup

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/SetupViewModel.cs`
- Create: `src/Lego2STL.Gui/Views/SetupView.axaml` (+ `.axaml.cs`, carrying the four file-picker
  handlers `InputView` and `OptionsView` have today)
- Create: `tests/Lego2STL.UiTests/SetupTests.cs`

**Interfaces produced:**

```csharp
public sealed partial class SetupViewModel : ViewModelBase
{
    public SetupViewModel(RunOptionsViewModel options);
    public RunOptionsViewModel Options { get; }
    public OptionRowsViewModel Rows { get; }
    public string? Problem { get; }
    public bool CanStart { get; }
    public string CommandLine { get; }
    public int ChangedCount { get; }                  // the rail's badge
    public string? PlannedFolder { get; }             // RunLayout.Plan(...)?.Root
    public bool Scanning { get; set; }
    [RelayCommand] private Task ScanPagesAsync();      // moved from MainViewModel unchanged
    [RelayCommand] private void Start();               // raises Started
    public event EventHandler? Started;                // the shell begins the run
    /// <summary>Fills --log with the planned folder's run.log while the user has not set one.</summary>
    public void FollowTheInput();
}
```

**The one behaviour change, stated plainly.** `FollowTheInput` runs whenever anything that feeds
`RunLayout.Plan` changes — kind, input path, set number, output directory. When `Options.LogFile`
is null **or** equal to the previously planned `run.log`, it is set to
`RunLayout.Plan(settings)?.LogPath`; otherwise it is left alone, so a log the user chose is
never overwritten and a cleared one stays cleared. It is therefore visible in the shown command
line, the log survives a crash of the window, and a terminal user running the shown line writes
the same file. The CLI's own default is untouched.

`Start` lives here and nowhere else — that single home is what makes the wrong-screen problem
structurally impossible, and it is why `Start` is unreachable from an empty Runs list.

`SetupView` is today's `InputView` markup — the three radio buttons, the document / parts list /
set number blocks, `--include-spares`, `--color-scheme`, the page range and the scan button —
above an `OptionListView` bound to `Rows`, with the problem card and the `Start` button at the
foot.

- [ ] **Step 1:** Write `SetupTests`: a fresh Setup cannot start and says why; choosing a parts
  list makes it startable; `--log` follows the input and lands inside the planned folder; a
  `--log` the user typed survives a change of input; clearing it leaves it clear; `ChangedCount`
  counts the changed rows; `Start` raises `Started` only when it can; and the view draws in both
  languages.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write the view model and the view.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: one screen to set a run up, and the log goes in the run's folder`.

---

## Task 14: The open run

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`
- Create: `src/Lego2STL.Gui/Views/RunDocumentView.axaml` (+ `.axaml.cs`)
- Modify: `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/CatalogueView.axaml` — `x:DataType` becomes
  `vm:RunDocumentViewModel`; the translucent `#33FFAA00` band becomes `AppWarningBand` /
  `AppWarningBorder` / `AppWarningText`
- Modify: `src/Lego2STL.Gui/ViewModels/MainViewModel.cs` — `ShowCatalogue` delegates, so the
  build and `CatalogueTests` stay green until Task 17
- Create: `tests/Lego2STL.UiTests/RunDocumentViewTests.cs`

**Interfaces produced:**

```csharp
public sealed partial class RunDocumentViewModel : ViewModelBase, IDisposable
{
    /// <summary>A run being watched as it happens.</summary>
    public static RunDocumentViewModel Live(RunSettings settings, RunLayout layout);

    /// <summary>A run read back off the disk. Its live facet is inert.</summary>
    public static RunDocumentViewModel Reopened(RunFolder folder);

    public RunDocument Document { get; }               // the projection; replaced, never edited
    public string Name { get; }
    public string StatusText { get; }                  // one of the four words
    public string RailText { get; }                    // "61%" while live, the status word after
    public bool IsLive { get; }

    // ---- the live facet, inert on a reopened run ----
    public double Progress { get; }
    public string StageText { get; }
    public ObservableCollection<string> Log { get; }
    public bool Busy { get; }
    [RelayCommand] private void Cancel();

    // ---- the catalogue ----
    public ObservableCollection<CataloguePartViewModel> Parts { get; }
    public ObservableCollection<string> Colours { get; }
    public string? ColourFilter { get; set; }
    public string? Search { get; set; }
    public IEnumerable<CataloguePartViewModel> VisibleParts { get; }

    [RelayCommand] private void OpenFolder();
    [RelayCommand] private void OpenPartsList();
    public event EventHandler<RunSettings>? ContinueRequested;   // the shell starts the next run
    [RelayCommand] private void ContinueFromPartsList();          // needs-a-decision exit
    [RelayCommand] private void RunItAgain();                     // pre-manifest folders

    public Task RunAsync(CancellationToken outer);      // live only; drives PipelineRunner
}
```

`CataloguePartViewModel`'s constructor becomes
`CataloguePartViewModel(RunDocumentPart part, string? shapePath, string? platePath)`. This is a
deviation from the spec's "unchanged", and a forced one: the spec's own measured facts say the
two shape warnings cannot be recovered from disk, so they are read from the manifest rather than
recomputed from a `PreparedMesh` a reopened run does not have. `HasOpenEdges`, `HasThinFeatures`,
`Size` and `Title` come from `RunDocumentPart`; everything else — the swatch, the picture, the
two open buttons, `Matches` — is untouched.

**Defect 1, folded in.** `StatusText` is one of four words, never "Done" for a run that could
not be verified. On a needs-a-decision run the shell stays on the run's page, `Progress` is
`Document.Progress` — where it actually stopped — and an amber card names the cause from
`Document.Unread` or `Document.Failed`, each failure with its reason, offering **Open the parts
list** and **Continue from the parts list**, which lands in the same folder via `RunLayout.For`.
The window and the CLI's exit code 2 finally agree.

**Defect 3, folded in.** There is no `Log.Clear()`: a new run is a *new document*, so the
previous run's log survives on its own page and in `run.log`. `OpenFolder` cannot lie because
the manifest — and the folder — exist from second one.

`RunAsync` opens `RunLogFile.Open(settings.LogFile)`, appends every line to both the file and
`Log` (the panel skipped when `Quiet`, with `UiQuietNote` shown in its place, exactly as
`ConsoleRun.Say` does for a console), records the index, runs `PipelineRunner`, and replaces
`Document` from `RunManifest.From(outcome, …)` — the same projection a reopened run uses, so the
two cannot drift.

`RunDocumentView` is a progress panel — the bar, the stage, the stored command line in the
header, the folder button — then the amber card, then the log inside a `ThemeVariantScope`
forced to `Dark` so it is a terminal in both themes with a scrollbar that matches it, then
today's `CatalogueView`.

- [ ] **Step 1:** Write `RunDocumentViewTests`: **a reopened run and a live run render the same
  catalogue** — build an outcome, project it, write and re-read the manifest, and assert the two
  view models' `Parts` are equivalent on part number, colour, quantity, size and both warnings;
  a needs-a-decision document shows the amber card and a progress under 1; a `Complete` document
  shows none; `StatusText` is the right word for each of the four statuses and never `UiDone` for
  `NeedsDecision`; a pre-manifest folder shows `UiNoManifest` and offers `UiRunItAgain`; a
  newer-version manifest shows `UiNewerManifest`; the colour filter and the search still narrow
  the list.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write the view model, the view, and the `CataloguePartViewModel` change; make
  `MainViewModel.ShowCatalogue(RunOutcome)` project through the manifest so `CatalogueTests`
  keeps passing.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: a run has a page of its own, and it stops reporting "done" when it is not`.

---

## Task 15: Runs

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/RunsViewModel.cs`, `src/Lego2STL.Gui/ViewModels/RunRowViewModel.cs`
- Create: `src/Lego2STL.Gui/Views/RunsView.axaml` (+ `.axaml.cs`)
- Create: `tests/Lego2STL.UiTests/RunsTests.cs`

**Interfaces produced:**

```csharp
public sealed partial class RunsViewModel : ViewModelBase
{
    public ObservableCollection<RunRowViewModel> Rows { get; }
    public bool Loading { get; }
    public int Count { get; }                              // the rail's count
    public Task RefreshAsync();                            // reads N small manifests off the thread
    [RelayCommand] private void ForgetEverything();
    public event EventHandler<RunFolder>? OpenRequested;
}

public sealed partial class RunRowViewModel : ViewModelBase
{
    public string Folder { get; }
    public string Name { get; }
    public bool Missing { get; }                            // greyed; only forget
    public string StatusText { get; }
    public string Summary { get; }                          // counts, or "stopped while ..., 38 of 91"
    public string When { get; }
    public string Source { get; }
    [RelayCommand] private void Open();
    [RelayCommand] private void Forget();
}
```

**The index is a convenience, the folder is the truth**: every field a row displays is read from
that folder's own `run.json`. `RefreshAsync` reads `RunIndex.Read()`, then
`Task.Run(() => RunFolder.Read(path))` per row, filling rows in as they arrive — exactly the
pattern `MainViewModel.LoadPicturesAsync` already uses for thumbnails. Rows whose folder is gone
are greyed and offer only *forget*. There is no cap on history size: *forget* and *forget
everything* are enough until there is evidence otherwise.

**Adopting a folder.** Runs also offers *Open a run folder* — a folder picker for a run that is
not in the index, which is how a folder copied from another machine, or one from before the
index existed, gets a row. A folder holding neither a `run.json` nor a `<name>.csv` is **refused,
with a reason** rather than added as an empty row: `RunFolder.Read` reports both absent, and the
screen says so in place of adding it. Adopting a folder that qualifies calls
`RunIndex.Record(RunLayout.At(folder))` and opens it. Two more members:

```csharp
    [RelayCommand] private Task AdoptFolderAsync(string folder);
    public string? AdoptProblem { get; }               // why the last folder was refused
```

- [ ] **Step 1:** Write `RunsTests` with `AppDataDirectory.Variable` in a temp folder: an empty
  index draws `UiRunsEmpty`; two recorded folders give two rows, newest first, with their status
  words; a row whose folder was deleted is `Missing` and its only command is `Forget`; `Forget`
  drops it from the index and from the list; `ForgetEverything` empties both; a folder with a
  `running` manifest reads as `UiStatusRunning`; and a stopped run's `Summary` names the stage it
  reached. Adopting a folder with a parts list but no manifest adds a row; adopting a folder
  with neither is refused and `AdoptProblem` says why; adopting a folder already in the index
  does not duplicate it.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write the two view models and the view.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: the runs that have happened are a screen`.

---

## Task 16: Settings

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/SettingsViewModel.cs`
- Create: `src/Lego2STL.Gui/Views/SettingsView.axaml` (+ `.axaml.cs`, carrying the `--log` picker)
- Modify: `tests/Lego2STL.UiTests/ViewLocatorTests.cs` — add
  `The_same_view_model_gets_the_same_view` from Task 1, now that `SettingsView` exists
- Create: `tests/Lego2STL.UiTests/SettingsTests.cs`

**Interfaces produced:**

```csharp
public sealed partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel();                            // for the locator test and the designer
    public SettingsViewModel(RunOptionsViewModel options, UserSettings saved, RunsViewModel runs);
    public RunOptionsViewModel Options { get; }            // --api-key, --log, --quiet
    public IReadOnlyList<LanguageChoice> Languages { get; }
    public LanguageChoice SelectedLanguage { get; set; }    // --lang, its one home
    [RelayCommand] private void ForgetEveryRun();
}
```

The four that are about this machine rather than this run: `--api-key`, `--log`, `--quiet`, and
`--lang`, which is outside the 22. Nothing appears on both screens — that is also what removes
today's duplicated language menu. The header menu stays, because a language switch has to be
findable when you cannot read the rail.

- [ ] **Step 1:** Write `SettingsTests`: the four flags are named on the screen; typing a key
  reaches `ToSettings().ApiKey`; ticking `--quiet` reaches `ToSettings().Quiet`; choosing Italian
  changes the window at once and is remembered in `UserSettings`; `ForgetEveryRun` empties the
  index. Then add the Task 1 locator test.
- [ ] **Step 2:** Run and watch it fail to compile.
- [ ] **Step 3:** Write the view model and the view.
- [ ] **Step 4:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 5:** Commit — `feat: what belongs to this machine has a screen of its own`.

---

## Task 17: The rail

The wall comes down. `Screen`, `ShowCommand`, `OnInput`, `OnOptions`, `OnRun` and `OnCatalogue`
are deleted: rail selection is the state.

**Files:**
- Rewrite: `src/Lego2STL.Gui/ViewModels/MainViewModel.cs` (shell only, well under 250 lines)
- Rewrite: `src/Lego2STL.Gui/Views/MainWindow.axaml` (+ `.axaml.cs`)
- Delete: `src/Lego2STL.Gui/Views/InputView.axaml`, `OptionsView.axaml`, `RunView.axaml` and
  their `.axaml.cs`
- Modify: `src/Lego2STL.Gui/App.axaml.cs` if the shell's construction changes

**Interfaces produced:**

```csharp
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    public RunsViewModel Runs { get; }
    public SetupViewModel Setup { get; }
    public SettingsViewModel Settings { get; }
    public RunDocumentViewModel? OpenRun { get; }
    public RunOptionsViewModel Options => Setup.Options;   // the footer and the tests
    public ViewModelBase Current { get; }                  // what the body shows

    public bool ShowingRuns { get; }                       // derived from Current, not a parallel enum
    public bool ShowingSetup { get; }
    public bool ShowingOpenRun { get; }
    public bool ShowingSettings { get; }

    public void Show(ViewModelBase screen);
    [RelayCommand] private void ShowRuns();
    [RelayCommand] private void ShowSetup();
    [RelayCommand] private void ShowOpenRun();
    [RelayCommand] private void ShowSettings();
    [RelayCommand] private void NewRun();                  // the rail's foot
    public LanguageChoice SelectedLanguage { get; set; }    // the header menu
}
```

The rail, 200 px open — sized for `Esecuzione` plus a status of `Non riuscito`. The selected row
is a 3 px accent bar plus a surface fill, not a full accent tint. Four rows: Runs with its count,
Setup with its count of non-default options or a problem badge, the open run with its name and
progress or status, and Settings. `[ New run ]` at the foot.

**The conflict between the two chosen proposals, resolved and recorded.** The rail foot carries
*New run*; `Start` lives on Setup. `Start` is therefore unreachable from an empty list, which was
the original complaint.

The body is a `ContentControl Content="{Binding Current}"`, resolved by the cached
`ViewLocator` from Task 1 — which is why each screen keeps its own view and the log its scroll
position. The footer shows Setup's live command line while `ShowingSetup`; a run's page shows
that run's **stored** line in its own header, so a run from three weeks ago can be reproduced in
a terminal without reconstructing what was ticked.

Starting a run: `Setup.Started` → build the settings, `RunLayout.Plan`, `RunIndex.Record`,
`OpenRun = RunDocumentViewModel.Live(...)`, `Show(OpenRun)`, `await OpenRun.RunAsync(...)`,
`Runs.RefreshAsync()`. `RunDocumentViewModel.ContinueRequested` and a row's `OpenRequested` are
wired the same way.

- [ ] **Step 1:** Rewrite `MainViewModel` and `MainWindow.axaml`; delete the three old views.
- [ ] **Step 2:** Run: `dotnet build Lego2STL.slnx`. Expected: FAIL in the UI tests, which still
  name `Screen`. That is Task 18; confirm the two `src` projects build.
- [ ] **Step 3:** Run: `dotnet build src/Lego2STL.Gui`. Expected: PASS.
- [ ] **Step 4:** Commit — `feat: a rail over the run folder, in place of four tabs that were not tabs`.

---

## Task 18: Twelve tests move at once

They are the safety net for a full layout rewrite. The `ViewLocator` cache and the
`command.Options` rewrite landed first, in Tasks 1 and 2, so the net was re-hung before the wall
came down.

**Files:**
- Modify: `tests/Lego2STL.UiTests/WindowTests.cs`, `CatalogueTests.cs`, `OptionParityTests.cs`

Re-pointed: `All_four_screens_draw` and `A_picture_of_each_screen_is_written` follow the new rail
— Runs, Setup, the open run, Settings — driven by `model.Show(...)` instead of `model.Screen`.
`No_label_is_showing_the_name_of_a_missing_phrase` and
`A_picture_of_each_screen_in_italian_is_written` likewise.
`Choosing_a_language_changes_the_window_at_once` asserts on the rail's own words, `Runs` and
`Esecuzioni`. `CatalogueTests` builds a `RunDocumentViewModel` from a pretend run rather than
calling `MainViewModel.ShowCatalogue`, which Task 17 removed.

`Every_option_the_command_line_takes_is_named_in_the_window` unions the text of **Setup and
Settings**, because the options now divide across the two, and runs with changed-only **off**.
It is strictly stronger than the array it replaced, which omitted `--include-spares` and
`--color-scheme` as well as `--quiet`.

Two new, already written in Tasks 14 and 12 and confirmed here: a reopened run and a live run
render the same catalogue; and switching rail items preserves the log's scroll position, which is
what stops the `ViewLocator` cache from silently regressing later.

- [ ] **Step 1:** Add `Switching_screens_keeps_the_logs_place` — scroll the run page's
  `ScrollViewer`, `Show(Setup)`, `Show(OpenRun)`, assert `Offset` is unchanged.
- [ ] **Step 2:** Re-point the rest.
- [ ] **Step 3:** Run: `dotnet test Lego2STL.slnx`. Expected: PASS.
- [ ] **Step 4:** Commit — `test: the suite follows the rail`.

---

## Task 19: Verification

- [ ] **Step 1:** `dotnet build Lego2STL.slnx -c Release` — both target frameworks, no warnings
  introduced.
- [ ] **Step 2:** `dotnet test Lego2STL.slnx` — everything green, and the count is at or above
  the 362 the branch started from.
- [ ] **Step 3:** `dotnet test Lego2STL.slnx --collect:"XPlat Code Coverage"` — 80% or better.
- [ ] **Step 4:** `LEGO2STL_UI_SHOTS=<dir> dotnet test tests/Lego2STL.UiTests` and look at the
  pictures, in both languages, light and dark: the rail, the four screens, an amber
  needs-a-decision card, a filled catalogue.
- [ ] **Step 5:** Run the real window once against the reference document and check by hand: the
  run appears in Runs while it is still going; killing the window mid-run leaves a row that reads
  *stopped*; reopening that row shows the same page; the shown command line names the run's own
  `run.log` and that file exists.
- [ ] **Step 6:** Check every file added or changed is under 800 lines and nothing new is much
  over 250.
- [ ] **Step 7:** Append to `PROGRESS.md` and commit.

---

## What this deliberately does not do

Carried over from the spec, so a reviewer does not read them as omissions: no virtualised card
grid (`UniformGridLayout` is absent from this project's Avalonia and would be a new package —
the catalogue keeps `ItemsControl` + `WrapPanel`); no review screen
(`RunLayout.ReviewDirectory` and `OverridesPath` stay unwritten); no in-app theme switch; no
application icon; no cap on history size. The *Find the catalogue pages* button showing its own
`extract --list-pages` line is optional and droppable.
