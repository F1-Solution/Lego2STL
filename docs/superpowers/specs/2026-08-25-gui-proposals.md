# What the window could look like, and how it could feel

**Date:** 2026-08-26
**Status:** proposals, for the user to choose from. Nothing here is implemented.

Four proposals for how the window is laid out and coloured, and three for how a run is
carried out from beginning to end. They came from two agents dispatched in parallel — the
Agency plugin's **UI designer** and **UX architect** — against the constraints in
`docs/superpowers/plans/2026-08-25-light-installers.md`, Task 12.

Implementing whichever is chosen is a separate plan, and it starts with brainstorming, not
with editing `.axaml`.

---

## Part 0 — The window as it is today

Written before the agents were dispatched, from the source, so that every proposal below can
be read against what it actually changes. File and line references are to
`src/Lego2STL.Gui/`.

### The frame

`MainWindow.axaml` is a `DockPanel` with three parts, 1040×720 at start and never smaller
than 820×560:

```
┌──────────────────────────────────────────────────────────────────────┐
│ [Input] [Options] [Run] [Catalogue]              Language [ Italiano ▾]│  ← header
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│                    whichever of the four screens                     │  ← body
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│ The same thing from the command line                                 │  ← footer
│ lego2stl build parts.csv --printer Bambu --scale 100    [Cancel][Start]│
└──────────────────────────────────────────────────────────────────────┘
```

- **Header.** Four `Button`s styled as tabs — a transparent background and a 2 px bottom
  border that takes `SystemAccentColor` when the screen is the current one. Not a `TabControl`.
  On the right, the language menu.
- **Body.** All four `UserControl`s are alive at once inside one `Panel`, shown and hidden by
  `IsVisible`. There is no navigation, no history, and no transition; switching screens is
  three property changes.
- **Footer.** A hint, then the equivalent command line as a `SelectableTextBlock` in a
  monospace stack, recomputed on every option change. Then `Cancel` — visible only while a run
  is going — and `Start`, which carries the `accent` class and is enabled when
  `Options.CanRun`.

Every screen shares one `DataContext`: the single `MainViewModel`. The four views bind into
it directly, and `CatalogueView`'s cards bind into `CataloguePartViewModel`.

### The four screens

| Screen | What it is for |
|---|---|
| **Input** | Which of three ways in: a document, a parts list, or a set number. Choosing one reveals only that one's fields. A document adds a page range, a **Find the catalogue pages** button that scans the PDF for label rows, and which colour numbering the document prints. A set number adds `--include-spares`. |
| **Options** | Every command-line option, in six labelled groups — Stages, Output, Geometry, Shape library, Plates, Behaviour. Roughly 20 controls in a single scrolling column, each labelled with its literal flag name (`--weld-tolerance  (mm)`) and, for most, a grey sentence of help beside it. |
| **Run** | A progress bar, one line of stage text, an **Open the folder** button that appears once there is an outcome, a card for the failure if there was one, and the log as a monospace, non-wrapping list. |
| **Catalogue** | A colour filter, a clear button, a search box, and a `WrapPanel` of fixed 264 px cards. Each card: a 104 px picture (or a flat colour swatch while the picture is being fetched, or forever if offline), a colour chip, the part number, `×n`, the colour name, the size, an amber warning band when the shape has open edges or features thinner than a 0.4 mm nozzle, and two half-width buttons — **Open the shape file**, **Open the plate**. |

### How a run happens today

1. The user picks a screen. There is no order — Options and Catalogue are reachable before
   anything has been chosen.
2. Anything that would stop the run is shown **on the Input screen**, in a bordered card, as
   the user types. `Start` is disabled while a problem stands.
3. `Start` is pressed. It is in the footer, so it is reachable from any of the four screens.
4. The view model clears the log, sets `Busy`, and **switches to Run**. The work happens on a
   worker thread; `Cancel` appears and cancels a `CancellationTokenSource`.
5. Progress arrives as a fraction plus a stage name and a detail string, which are joined with
   a dash into the one line under the bar. Log lines are posted to the UI thread.
6. On success the window **switches itself to Catalogue** and starts fetching part pictures in
   the background, one after another, so the list appears at once and fills in.
7. On failure it stays on Run, with the failure in a card.
8. Chosen directories, the printer and the language are remembered between sessions;
   everything else is not.

### Facts that constrain any proposal

Established from the source, not assumed:

| Fact | Where |
|---|---|
| Avalonia **12.1.1**, `FluentTheme`, `RequestedThemeVariant="Default"` — the window already follows the system's light/dark setting, and there is no in-app theme switch. | `App.axaml`, `Lego2STL.Gui.csproj` |
| The only accent is `SystemAccentColor`, taken from the OS. There is no palette of the program's own. | `MainWindow.axaml` |
| `Avalonia.Fonts.Inter` is already referenced and installed via `.WithInterFont()`. Inter is available without adding a dependency. | `Program.cs` |
| The app icon is still Avalonia's default `avalonia-logo.ico`. | `Assets/` |
| **Reading a document is Windows-only.** `OcrEngines.IsAvailable` is `false` off Windows and `Create` throws `OcrUnavailableException`. | `Core/Ocr/OcrEngines.cs` |
| **The window never mentions that.** Nothing in the GUI reads `IsAvailable`. On Linux and macOS the *A document* radio button is offered exactly as on Windows, and the run fails part-way with a message from Core. This is a real defect today, not a hypothetical. | `Gui/**` — no reference |
| Every label comes from `Strings.English.cs` / `Strings.Italian.cs` through `Loc.Current[key]`, and the language can be changed while the window is open. | `Localization/Loc.cs` |
| The language menu exists **twice** — in the header and again in Options → Behaviour — both bound to the same property. | `MainWindow.axaml`, `OptionsView.axaml` |
| Italian labels run longer than the English: *Find the catalogue pages* → *Trova le pagine del catalogo* (+38%), *Colour numbering in the document* → *Numerazione colori usata nel documento*, *Failed* → *Non riuscito*. Any fixed-width control has to be sized for the Italian. | `Strings.*.cs` |
| **Fourteen UI keys already exist in both languages and are used nowhere:** `UiTitle`, `UiNext`, `UiBack`, `UiSettings`, `UiCopyCommand`, `UiCopied`, `UiProgress`, `UiShowLog`, `UiHideLog`, `UiSortBy`, `UiSortByPart`, `UiSortByQuantity`, `UiSortByColour`, `UiQuantity`. A proposal that wants a wizard, a copy button, a collapsible log or a sort menu can have it **without adding a single key**. | verified by search across `Gui/**` |
| The `Problem` card lives on Input, but the option it complains about may be on Options — a bad `--plate-size` is reported on a screen that does not show `--plate-size`. | `InputView.axaml`, `RunSettings.Problems()` |

### What the window knows but does not say

Not a matter of taste, and worth stating before any proposal is read, because two of them turn
out to depend on it. A run has three outcomes, not two — `RunResult.Complete`,
`RunResult.Unverified`, `RunResult.Failed` — and `Unverified` is a real state with two causes:

- some part labels could not be read out of the document, so the parts list was written and the
  run **stopped there**, producing no shapes at all (`PipelineRunner.cs:96`); or
- some parts produced no shape, so the plates were **skipped entirely** — "a plate that
  quietly leaves out the parts that failed looks finished and is not" (`PipelineRunner.cs:344`).

The command line honours this: `ConsoleRun.cs:122` returns exit code **2** for an unverified
run. The window does not. `MainViewModel.StartAsync` maps `Complete` and `Unverified` to the
**same word**, `UiDone`; fills the progress bar to 100% for both; and switches away from the
Run screen to the Catalogue for both — away from the one log line that said the plates were
skipped (`MainViewModel.cs:225-237`).

`RunOutcome` also carries `Unread` (which labels could not be read), `Failed` (which parts
produced nothing, each with its reason) and `Notes`. **The window binds to none of the three.**
The terminal prints them; the window throws them away.

This is the largest gap between the window and its command-line twin, and it is not a layout
problem. Any UX proposal below that does not address it is leaving the program's worst
behaviour in place.

---

## Part 1 — Four proposals for how it could look

From the **UI designer**. Each is a different structure, not four repaints of the same window.

### What was checked before any of this was believed

The agent asserted 22 contrast ratios and a list of Avalonia controls. Both were checked rather
than taken on trust, and two things came back that change how these should be read.

**The contrast arithmetic is right — 21 of 22 exactly, to two decimal places.** The one
exception is in *The Bench*: its light warning text `#9A6400` was measured against the window
background (4.76:1, which is what the proposal reports) and not against the warning band it
actually sits on, `#FBEAD1`, where it is **4.24:1** and fails the 4.5 floor the proposal claims
to clear everywhere. It is small and fixable — `#8A5300` on the same band gives 5.36:1 — and
the other three schemes' warning pairs all pass, as do all sixteen secondary-text pairs across
the four. It is recorded rather than quietly corrected because the proposal explicitly promises
the floor.

**`UniformGridLayout` does not exist in this project's Avalonia.** The agent flagged this itself
as the one thing to confirm, and the answer is no: `UniformGridLayout` is in **none** of the 23
Avalonia assemblies the build produces, and no `Avalonia.Controls.ItemsRepeater` package is
restored. `VirtualizingStackPanel` is present, but it stacks — it does not wrap. So the
virtualised card grid proposed by *The Bench* and *The Console* **is a new dependency**, which
both of them say they do not need. Either they keep today's `ItemsControl` + `WrapPanel` and
give up virtualisation, or the user is asked for a package. Verified present and safe to use:
`SplitView`, `TransitioningContentControl`, `Carousel`, `Expander`, `ThemeVariantScope`,
`GridSplitter`, `TabControl`, `SplitButton`, `MaxLines`, `ExtendClientAreaToDecorationsHint`.

**One caveat the agent raised is correct and worth keeping.** `OcrEngines.IsAvailable` is
`#if WINDOWS`, and `Directory.Build.props` confirms `WINDOWS` comes from the
`net10.0-windows10.0.19041.0` *target*, not the running OS. A plain `net10.0` build has no
recogniser even on Windows. A chip reading *Windows only* is true of the three shipped
installers and false of a plain build; the agent's alternative, *Not in this build*, is true of
both. That is the safer string.

All four converge on two new keys, `UiWindowsOnly` (or `UiNotAvailableHere`) and
`UiOcrWayRound`, and all four note that `ErrOcrUnavailable` already exists in both languages and
is used nowhere. `UiOcrWayRound` would translate a sentence that is currently a **hardcoded
English string inside `OcrEngines.Create`**, so adding it closes a real localisation hole rather
than inventing copy. Separately, all four point out that `RunSettings.Problems()` returns
hardcoded English too (*"Choose a document to read."*), which any of them would have to fix
first.

---

### 1. The Bench

**Who it suits** — someone who wants the whole job visible as one page they work down, the way
the command line is one line they type left to right.

The four fake tabs disappear. The `Panel` of four `IsVisible`-toggled views becomes one
`ScrollViewer` over a `StackPanel` of four `Expander`s — Input, Options, Details, Catalogue — in
pipeline order, each header carrying a one-line summary on the right. `Screen` and the four
`OnX` properties survive as the `IsExpanded` bindings, so the view models barely move.

The Options summary is **the non-default flags themselves** (`--scale 120 --repair`) rather than
an English sentence, because flag names are identical in both languages — a summary that needs
no new key at all.

```
+----------------------------------------------------------------------+
| Lego2STL                              Language [ English      v ]    |
+----------------------------------------------------------------------+
|                                                                      |
|  What are you starting from?                                         |
|                                                                      |
|   ( ) A document                                     [ Windows only ]|
|       Not available in this build.                                   |
|   (o) A parts list                                                   |
|   ( ) A set number                                                   |
|                                                                      |
|   Parts list                                                         |
|   [ C:\sets\42100\parts.csv                     ]    [ Browse... ]   |
|                                                                      |
| .....................................................................|
|  v  Options                              --scale 120 --repair        |
| .....................................................................|
|  >  Details                              Ready                       |
| .....................................................................|
|  >  Catalogue                            Nothing yet.                |
| .....................................................................|
|                                                                      |
+----------------------------------------------------------------------+
| The same thing from the command line   [ Copy ]                      |
| lego2stl build "C:\sets\42100\parts.csv" --printer   [    Start    ] |
| prusa-mk4                                                            |
+----------------------------------------------------------------------+
```

Inside Options the six existing `UiGroup*` headings become nested `Expander`s, so twenty options
never arrive at once. During a run the sections above Details fold themselves away.

It also repairs something small and real: today `TextWrapping="Wrap"` on a growing command line
makes the whole footer change height and the body reflow every time an option is ticked.
`MaxLines="2"` with ellipsis, and the untrimmed text on the Copy path.

**Palette — warm workbench**

| Token | Light | Dark |
|---|---|---|
| Window background | `#FBF9F6` | `#171512` |
| Surface / card | `#FFFFFF` | `#1F1D19` |
| Card border | `#E3DDD3` | `#34302A` |
| Primary text | `#1C1A17` | `#F2EEE7` |
| Secondary text | `#5F5850` | `#A79E92` |
| Accent | `#B4501A` | `#F08A48` |
| Accent hover | `#963F12` | `#FFA469` |
| Text on accent | `#FFFFFF` | `#241004` |
| Success | `#1F6B3C` | `#64C68A` |
| Warning text | `#9A6400` *(→ `#8A5300`, see above)* | `#E0A93C` |
| Warning band / border | `#FBEAD1` / `#E0BE7F` | `#3A2A0E` / `#6B4E14` |
| Danger | `#B3261E` | `#F2857D` |
| Log background / foreground | `#F3EFE9` / `#26221D` | `#100E0C` / `#E8E2D8` |

Primary text on background 16.52:1 light, 15.76:1 dark. Text on accent 5.12:1 and 7.33:1.

The warning band is **opaque per theme**, not today's `#33FFAA00` — an alpha over an unknown
surface is exactly why the current band goes muddy the moment the OS is in dark mode.

**Type** — Inter. Heading 22/600, group 13/600 secondary, body 14/400 at 1.45, label 14/400,
hint 12/400, monospace 12.5/400. Italian: the summary column must hold `Non riuscito` against
`Failed` — reserve 150 px, not `Auto`; `Nascondi i dettagli` (19) needs `MinWidth="150"` or the
toggle resizes as it toggles.

**Windows-only note** — on the radio row itself, always visible, never hidden: the option stays
listed and stays clickable, rendered disabled with the chip on the same row and
`ErrOcrUnavailable` beneath it.

**What it costs** — `MainWindow.axaml` rewritten; the four views survive as `Expander` contents
once their own `ScrollViewer` roots come out, or you get nested scrolling. Named honestly by the
agent: one long page means the outer `ScrollViewer` measures an enormous visual tree on a large
set; `Expander` animates its content height, so a catalogue of hundreds of cards visibly
stutters on first open; and there is no icon set in the project, so every disclosure arrow is
the `Expander` chevron or a hand-drawn `Path`.

---

### 2. The Rail

**Who it suits** — someone who runs this often, wants the four places one click apart, and wants
`Start` reachable no matter where they are.

A `SplitView` replaces the `Panel` of four. The rail is a `ListBox` of five items — the four
screens plus `UiSettings` — with `SelectedIndex` bound to `Screen`. **That deletes
`ShowCommand`, `OnInput`, `OnOptions`, `OnRun` and `OnCatalogue` outright: selection is the
state.** Each rail row carries a status on the right — `UiDone`, the count of non-default
options, `StageText`, the part count. No new keys for any of it.

```
+----------------------------------------------------------------------+
| Lego2STL                                  Language [ English    v ]  |
+------------------+---------------------------------------------------+
|                  |                                                   |
|  Input      done | Input                                             |
|  Options       3 |                                                   |
|  Run       Ready | What are you starting from?                       |
|  Catalogue     - |                                                   |
|                  |  ( ) A document                    [Windows only] |
|  Settings        |      Not available in this build. Read the        |
|                  |      document on Windows once, bring the parts     |
|                  |      list here, or start from a set number.        |
|                  |                                                   |
|                  |  (o) A parts list                                 |
|                  |  ( ) A set number                                 |
|                  |                                                   |
|                  | Parts list                                        |
|                  | [ C:\sets\42100\parts.csv ]     [ Browse... ]     |
|                  |                                                   |
| [    Start    ]  |                                                   |
+------------------+---------------------------------------------------+
| The same thing from the command line                      [ Copy ]   |
| lego2stl build "C:\sets\42100\parts.csv" --printer prusa-mk4         |
+----------------------------------------------------------------------+
```

The fifth rail item, `UiSettings`, takes `--api-key`, `--log` and the language out of the middle
of a twenty-row option list and gives them a home. `Start` sits at the rail's foot, spanning its
width, so it is present on all five screens; the footer keeps only the command line, stated
once, permanently, across the full width.

**Palette — cool neutral**

| Token | Light | Dark |
|---|---|---|
| Window background | `#F4F6F8` | `#0F1216` |
| Surface / card | `#FFFFFF` | `#171B21` |
| Card border | `#DDE2E8` | `#2A313A` |
| Primary text | `#14181D` | `#E8ECF1` |
| Secondary text | `#55606C` | `#9BA6B3` |
| Accent | `#0B6BCB` | `#6BA6F5` |
| Accent hover | `#0A5AAA` | `#8CBCFF` |
| Text on accent | `#FFFFFF` | `#06182E` |
| Success | `#16794C` | `#58C99A` |
| Warning text | `#8A5A00` | `#E2B04A` |
| Warning band / border | `#FDF0D2` / `#E3C77E` | `#2E2510` / `#5A4A1F` |
| Danger | `#C0271B` | `#F2867C` |
| Log background / foreground | `#1B1F24` / `#DDE3EA` | `#0B0E12` / `#D6DEE7` |

Primary text on background 16.45:1 light, 15.83:1 dark. Text on accent 5.28:1 and 7.13:1. All
pairs clear 4.5; the floor is accent-on-background at 4.87 light.

The log is **dark in both variants** — a deliberate island wrapped in a `ThemeVariantScope`, so
its scrollbar and selection highlight come out right instead of a light scrollbar on a black
field.

**Type** — Inter. Heading 20/600, group 12/700 secondary, body 14/400, rail item 13/500, hint
12/400, monospace 12/400. **No uppercase anywhere**, deliberately: Italian group headings would
give `QUANTITÀ`, and accented capitals are unreliable across the three platforms' fallback
chains. Italian also sizes the pane — `Esecuzione` plus a status of `Non riuscito` is about
176 px, so the 200 px pane holds; do not shrink it to 160 for the English.

**Windows-only note** — the radio stays visible and disabled, chip at the end of its row, and
both `ErrOcrUnavailable` and the way round render underneath, because this layout has the width
for the refusal *and* the remedy.

**What it costs** — the agent names its own biggest risk: today all four views are alive at
once, so the log's and the catalogue's scroll positions survive a tab switch. A
`TransitioningContentControl` fed a fresh view each time **resets both**. Cache four singleton
views in the `ViewLocator`, or leaving Run mid-build and coming back scrolls the log to the top.
It is also candid that the 48 px compact rail is digits, not icons, and that at 820 px it is
arguably worse than not collapsing at all — in which case cut the compact mode and its width
converter entirely.

---

### 3. The Conveyor

**Who it suits** — someone doing this for the first or fifth time, who wants the program to tell
them what the next decision is rather than show them twenty at once.

A step ribbon replaces the tabs; the body is a `Carousel` whose slide direction carries the
sense of forward and back. **The discs stay clickable, so this is a wizard you can leave** — a
fifth run does not have to walk four steps. `UiNext` and `UiBack`, unused today, are the
navigation strip; on step 2 `Next` *is* `Start`, which is exactly what `StartAsync` already does
when it sets `Screen = Screen.Run`.

```
+----------------------------------------------------------------------+
| Lego2STL                                    Language [ English   v ] |
+----------------------------------------------------------------------+
|                                                                      |
|   (1) Input --------- (2) Options -------- (3) Run ------- (4) Cat.  |
|       here                waiting             waiting        waiting |
+----------------------------------------------------------------------+
|                                                                      |
|  What are you starting from?                                         |
|                                                                      |
|   ( ) A document                                     [ Windows only ]|
|       Not available in this build. Read the document on Windows      |
|       once, bring the parts list here, or start from a set number.   |
|                                                                      |
|   (o) A parts list                                                   |
|   ( ) A set number                                                   |
|                                                                      |
|   Parts list                                                         |
|   [ C:\sets\42100\parts.csv                     ]    [ Browse... ]   |
|                                                                      |
+----------------------------------------------------------------------+
| > lego2stl build "C:\sets\42100\parts.csv" --printer pru...  [Copy] v|
+----------------------------------------------------------------------+
|                                                          [  Next  >] |
+----------------------------------------------------------------------+
```

The command line is an `Expander` opening upward, and **its header carries the line itself** — a
collapsed drawer would break the parity promise, so it is visible at all times and expanding
only gives the wrapped full text.

**Palette — high contrast, one confident accent**

| Token | Light | Dark |
|---|---|---|
| Window background | `#FFFFFF` | `#101315` |
| Surface / card | `#F6F7F9` | `#191D20` |
| Card border | `#D9DDE3` | `#2C3236` |
| Primary text | `#101418` | `#EDF1F3` |
| Secondary text | `#565F6B` | `#9AA5AC` |
| Accent | `#0E6E5C` | `#35C0A3` |
| Accent hover | `#0A5849` | `#55D8BC` |
| Text on accent | `#FFFFFF` | `#04231D` |
| Success | `#136B3F` | `#5CC98A` |
| Warning text | `#8A5300` | `#DDAE4B` |
| Warning band / border | `#FBEFD3` / `#E0BE7F` | `#2C2612` / `#5A4A1F` |
| Danger | `#B3231A` | `#F0857C` |
| Log background / foreground | `#EEF0F3` / `#1A1E22` | `#0A0D0F` / `#DCE3E7` |

Primary text on background 18.50:1 light, 16.41:1 dark — the highest of the four. Text on accent
6.17:1 and 7.30:1. **Every pair clears 4.5 with margin; the lowest anywhere is 6.04.** That
headroom is intentional: a wizard is what a first-time user meets first, and the accent doubles
as the done-tick in the ribbon, so it has to survive as a small filled disc as well as a large
button. Success sits very close to the accent because here `UiDone` and *go* mean the same
thing; only warning and danger break the green.

**Type** — Inter, one size up throughout: heading 26/600, body 15/400 at 1.5, label 14/400, hint
13/400, monospace 13/400. A step shows a fraction of what the others show and can afford it.
Italian and the ribbon is the tight spot: four names, four discs and three connectors in 820 px
fits, but only just — set the ribbon columns `Auto,*,Auto,*,Auto,*,Auto` so the connectors
absorb the difference between the languages, and let the sub-labels trim so `Non riuscito` never
widens a column.

**Windows-only note** — this layout has the most vertical room of any of the four, so the note is
a full three lines under a disabled but visible radio button, untruncated in either language.
**The agent names this as the single reason to prefer this proposal**, and says it needs the
least extra machinery: add the OCR check to `Problems()` and `Next` goes disabled with the
reason already on screen above it.

**What it costs** — the most new XAML of the four: a bespoke step ribbon, four step view models,
and a three-state primary button. The same view-caching problem as The Rail, and worse here
because sliding back and forth is the intended gesture. `PageSlide` composites both pages, so a
slide onto a catalogue of several hundred cards will drop frames on a modest Linux GPU. And the
ribbon's connectors cannot be drawn between the discs' centres in pure XAML — they are
`Rectangle`s in the spacer columns, which is a fake and will look slightly off when one step
name is much longer than its neighbour.

---

### 4. The Console

**Who it suits** — someone who already knows the command line, wants the settings and their
consequences visible in the same glance, and never wants to navigate to find out what happened.

**No screen switching at all.** A `Grid` with a `GridSplitter`: left is what you are asking for,
right is what came of it. `Screen`, `ShowCommand` and the four `OnX` properties are deleted, not
replaced.

```
+----------------------------------------------------------------------+
| Lego2STL   [ English  v ]      Ready               [     Start     ] |
+----------------------------------------------------------------------+
| What are you starting from?     | Progress                     idle   |
|  ( ) A document  [Windows only] | [..............................]    |
|      Not available in this      |                                     |
|      build.                     | +---------------------------------+ |
|  (o) A parts list               | | Ready                           | |
|  ( ) A set number               | |                                 | |
|                                 | +---------------------------------+ |
| Parts list                      |            [ Hide details ]         |
| [ C:\...\parts.csv ][Browse...] |-------------------------------------|
|                                 | Catalogue                           |
| v Stages                        | Colour [ All colours    v ]  [ x ]  |
|   [ ] --csv-only                | Sort by [ Part number   v ]         |
|   [ ] --no-plates               | Search [                         ]  |
| > Output                        |                                     |
| > Geometry                      | Nothing yet. Run the pipeline and   |
| > Shape library                 | the parts appear here.              |
| > Plates                        |                                     |
| > Settings                      |                                     |
|---------------------------------|                                     |
| The same thing from the command |                                     |
| line                   [ Copy ] |                                     |
| lego2stl build "...\parts.csv"  |                                     |
| --printer prusa-mk4             |                                     |
+----------------------------------------------------------------------+
```

The strongest argument for it: the command line sits at the foot of the **left** pane, directly
under the controls that generate it. The parity promise stops being a footnote at the bottom of
the window and becomes a live readout attached to the form — tick `--repair`, watch the flag
appear two inches below, one eye movement.

`UiNext` and `UiBack` are the two keys this proposal has **no** use for. It says so rather than
inventing a place for them.

**Palette — dark-first terminal**

| Token | Light | Dark *(the default intent)* |
|---|---|---|
| Window background | `#FFFFFF` | `#0D1117` |
| Surface / card | `#F6F8FA` | `#151B23` |
| Card border | `#D0D7DE` | `#2A323D` |
| Primary text | `#1F2328` | `#E6EDF3` |
| Secondary text | `#59636E` | `#9198A1` |
| Accent | `#9A6100` | `#E3A008` |
| Accent hover | `#7D4E00` | `#F5B429` |
| Text on accent | `#FFFFFF` | `#1A1200` |
| Success | `#12703E` | `#3FB950` |
| Warning text | `#8A5A00` | `#D29922` |
| Warning band / border | `#FFF3D3` / `#E0BE7F` | `#2A2008` / `#6B4E14` |
| Danger | `#C0271B` | `#F85149` |
| Log background / foreground | `#0D1117` / `#C9D1D9` | `#010409` / `#C9D1D9` |

Primary text on background 15.80:1 light, 16.02:1 dark. Text on accent 5.14:1 and 8.23:1.

Two decisions stated out loud. The log is dark in **both** variants — this is a console and the
log is its terminal. And the amber accent **cannot be shared across variants**: `#E3A008` under
white text is 2.26:1, unusable, so light drops to `#9A6100`. The agent says so rather than
shipping one amber and hoping, and it is right — amber is the one accent family that genuinely
cannot survive both grounds.

**Type** — the densest of the four, because two panes at once is the point: heading 15/600, body
13/400, label 12/400, hint 11/400, monospace 12/400 at 1.35. Part numbers, sizes and the command
line render monospace; names, colours and warnings render Inter. Italian decides the 380 px left
pane, and the agent pins `MinWidth="340"` on it so the splitter cannot go narrower. It also
flags its own trade: **at 11 px the hint text is at the floor of comfortable reading**, and if
any reviewer is over forty, raise the whole table a point and widen the pane to 400.

**Windows-only note — the weakest of the four, and it says so.** With only 380 px the chip and
`ErrOcrUnavailable` fit, but the way round moves into a `ToolTip` on the disabled radio button:
not reliably reachable in every Avalonia backend and **not reachable by keyboard at all**. The
proposal's own words: if the way round matters more than the density — and for a Linux user
meeting this window for the first time it does — this is the wrong proposal and The Conveyor is
the right one.

**What it costs** — the most deleted code of the four. But a `Grid` cannot reflow into a stack,
so at 820 px two panes of 380 and 436 are unusable without wrapping the left pane in an overlay
`SplitView` below about 1000 px. **This is the one proposal closest to breaking the 820 px
minimum**, and without that converter it does not meet it. Two splitters also mean two more
positions to persist, and a splitter that resets every launch is worse than no splitter.


---

## Part 2 — Three proposals for how it could feel

From the **UX architect**. One of the three is conservative by request, and it is named as such.

### What it found before proposing anything

The agent was asked to verify today's flow rather than take the briefing on trust, and came back
with nine defects — seven of them not in Part 0 above, because I had not found them. **Every one
that can be checked from the source was checked, and every one held.** They are listed first
because all three proposals are built on them.

| Claim | Verified |
|---|---|
| `RunSettings.Problems()` validates `PlateSize` unconditionally, not `if (WantsPlates)`. Tick `--csv-only`, leave a malformed bed size in the box, and `Start` is disabled for a run that would never arrange a plate. | Yes — `RunSettings.cs:211`; `WantsPlates` exists at line 137 and is not consulted. |
| `StartAsync` calls `Log.Clear()` but never clears `Outcome`. During a new run the **Open the folder** button is visible and points at the *previous* run's folder. | Yes — `MainViewModel.cs:200`, no assignment to `Outcome` before the run. |
| After a failure that button is visible and does nothing: `RunOutcome.Failure` leaves `Layout` null, and `OpenRunFolder` silently returns. | Yes — `RunOutcome.cs:69`, `MainViewModel.cs:249`. |
| After **Cancel**, `Outcome` is never assigned, so there is no route from the window to the parts list and the shapes already on disk. | Yes — the `OperationCanceledException` catch sets only `StageText`. |
| The terminal writes `MsgCouldNotReadEntriesMany` and `MsgWrittenWithoutThem` to stderr for an unverified run. The window is strictly less informative than its twin. | Yes — `ConsoleRun.cs:100-109`. |
| `RunLayout.For` and `PipelineRunner.RebrickableSetFolderName` are both `public static` and deterministic, so **the window could know the run folder before `Start` is pressed**. Nothing in the GUI uses this. | Yes — `RunLayout.cs:45`, `PipelineRunner.cs:395`. |
| `RunLayout` defines `ReviewDirectory` and `OverridesPath` and **nothing anywhere writes to either** — a review flow was designed and never built. | Yes — `RunLayout.cs:32,39`, referenced nowhere else in `src/`. |
| `--quiet` is a real command-line option with no control anywhere in the window. | Yes — `PipelineOptions.cs:133`; zero hits for `Quiet` in `Gui/`. |
| `--list-pages` is what the *Find the catalogue pages* button does, but `ToCommandLine` never emits it, so one thing the window does is invisible in the line the window shows. | Yes — `ExtractCommand.cs:27`; no `list-pages` in `ToCommandLine`. |

Two of those deserve pulling out.

**The parity promise is already 99%, not 100%, and the test that guards it cannot notice.**
`WindowTests.Every_option_the_command_line_takes_is_named_on_the_options_screen` walks a
**hardcoded array of 21 option names** and asserts each appears on the Options screen. `--quiet`
is not in the array. A test that enumerates what it checks cannot catch the option nobody added
to its own list, so it passes green while the promise it exists to defend is broken.

**`RunLayout.For` was written for the retry these proposals want.** Its own comment says so:
*"When the parts list of a previous run is the input, the existing run folder is reused rather
than nesting another inside it."* Two of the three offer *continue from the parts list* after a
failure; that is not a new mechanism, it is an existing one the window has never called.

One correction to the agent, and it matters for the Windows-only constraint: it observed that
`ScanPagesAsync` needs no recogniser — it runs `LabelLocator` over `PdfPageImageSource`, and
`PdfPageImageSource.IsSupported` is true on all three systems. **The *Find the catalogue pages*
button therefore works on Linux and macOS.** Only the reading of the labels is Windows-only.
Every proposal below relies on this, and it is correct.

---

### A. Guard Rail — *the conservative one*

Four screens, four tabs, one footer. Nothing moves. The gate, the log and the ending change.

1. Opens on **Input**, as today. The duplicate language menu in Options → Behaviour is deleted;
   `--lang` stays named on Options as a read-only line pointing at the header, so the parity
   test still finds it.
2. `Options.Problem` is **split by where the setting lives**: `InputProblems` and
   `OptionProblems`, each a filtered view of `Problems()`. Input's card shows only what Input
   owns; Options grows an identical card for its own. The **Options tab carries a count badge**
   when it has problems. That is the whole fix for a `--plate-size` complaint appearing on a
   screen that does not show `--plate-size`.
3. `Problems()` is changed in Core to skip the `PlateSize` check when `!WantsPlates`, so a
   `--csv-only` run stops being blocked by a bed size it will never use.
4. Options is reordered — Output, Plates, Geometry first, then `Expander`s for Shape library and
   Behaviour that remember their state. No option moves screen, none is removed.
5. `Start` is disabled *and explained* on Run and Catalogue; on a finished Catalogue it reads
   **Run it again**, admitted for what it is.
6. `RunLayout.For` is computed **before** the run and stored, so **Open the folder** works from
   second one — and after a failure, and after a cancel.
7. The log is no longer cleared. A rule and the command line are appended at the head of each
   run, making the Run screen a session transcript; `UiShowLog`/`UiHideLog` collapse it.
8. On `Unverified` the window **stays on Run**, shows an amber card naming how many entries went
   unread or how many parts produced nothing, and offers **Open the parts list** and **Continue
   from the parts list**. The catalogue is still filled — but the window no longer says "Done".

**Fixes** — `Start` from an empty Catalogue; the problem on the wrong screen; the `--csv-only`
false block; the folder button that lies during a run and is inert after a failure; the
destroyed log; `Unverified` reported as Done; the duplicated language menu; six of the fourteen
dead keys.

**Costs** — Options still has 21 controls in one column: reordered and partly folded, not
solved. A user who hits an Options-owned problem must still notice a badge and change tab.
**And the agent flags its own contradiction:** the `Problems()` change alters CLI behaviour too,
so a proposal sold as conservative contains a Core change. It says so rather than pretending the
window can fix it alone. `Every_option_...` survives;
`A_run_with_nothing_chosen_cannot_be_started_and_says_why` needs re-pointing at `InputProblems`.

**Worse off** — "nobody, materially. That is the argument for it and also its ceiling."

**A run that fails halfway** — log kept whole; folder button works because it never depended on
the outcome; **Continue from the parts list** re-enters as a parts-list run, and because
`RunLayout.For` reuses the folder whose name matches the parts list, the retry lands in the same
place instead of nesting. The document is not read twice.

**Windows-only** — a card under the radio holding `ErrOcrUnavailable` (already translated,
currently dead) plus two buttons that switch to the other two inputs. The radio stays selectable
and selected. The OCR check is added to `Problems()`, so `Start` is disabled with a reason
instead of failing at 20% — **and the CLI inherits the same early refusal.**

**The ~20 options** — nothing deleted, nothing hidden behind a mode; reordered by how often a
printing run touches them. The user gives up two clicks to reach `--weld-tolerance`. "This
proposal does not claim to have solved the option problem, only to have stopped it hurting."

**New keys** — 11, listed in full with Italian: `UiStartFromInput`, `UiRunAgain`, `UiStopped`,
`UiSomeEntriesUnread`, `UiPartsWithNoShape`, `UiOpenPartsList`, `UiContinueFromPartsList`,
`UiDocumentElsewhere`, `UiUsePartsListInstead`, `UiUseSetNumberInstead`, `UiLanguageInHeader`.
Revives 8 dead ones.

---

### B. The Wizard That Was Never Built

`UiNext` and `UiBack` exist in both languages and are used nowhere. This is what they were for.
Four screens become five steps; the tabs become a step rail.

**The agent opens with an objection to its own proposal**, which is worth quoting in substance:
a wizard is the right shape for the first run and the wrong shape for the fifth. This tool is run
over minutes on a set someone is iterating on — change the clearance, run again. *Any wizard
that makes the fifth run walk five steps is worse than what exists.* Everything is bent around
that: the rail is clickable, and **the window opens on step 5 from the second run onward**.

1. **Start from** — the three radios alone, large.
2. **What to make** — three outcomes, not checkboxes: *Print the whole set*, *Shapes, no plates*,
   *Just the parts list*. The same `--csv-only` / `--no-plates` flags stated as results rather
   than negations; the footer's command line changes as they are pressed, "which is where the
   user learns that *Just the parts list* **is** `--csv-only`."
3. **Printer and output** — five controls, every one of which owns a problem in `Problems()` or
   decides where files land. The plate controls **disappear**, not grey, when step 2 chose no
   plates — and with them the `--plate-size` check.
4. **Everything else** — the remaining thirteen plus `--quiet`, added to close the parity leak.
   Headed *Nothing here needs changing for a first run*, and skippable from step 3.
5. **Check and start** — a read-back plus the full-width command line. **`Start` lives only
   here.** Problems are listed here, each hyperlinked to the step that owns it.

**Fixes** — `Start` unreachable from where it does not belong; the wrong-screen problem becomes
*structurally impossible*; 20 options become 5 + 5 + 13 with the 13 declared skippable; the
Catalogue stops being a tab and becomes a result, so it cannot be a dead tab; `--quiet` added and
the parity claim becomes true.

**Costs — the sharpest and most specific warning of the whole exercise.**
`Every_option_the_command_line_takes_is_named_on_the_options_screen` **fails as written**,
because the options now live on three steps. And: *if the steps are put in a `TabControl` or
`Carousel` rather than four `IsVisible` panels, unselected content is never realised and
`GetLogicalDescendants` finds nothing.* Keep the `IsVisible`-toggled `Panel` or rewrite the test
to drive the step machine. Both screenshot theories are rewritten too.

**Worse off** — "the power user on run three who wants to flip `--offline` and go: four actions
where today it is two." And Italian is hardest here — five rail labels at `MinWidth="820"` do not
fit, so the rail must be numbers with the label only on the current step.

**A run that fails halfway** — Working becomes a **Stopped** step rather than falling back to a
screen. The log is written to the run folder through the existing `--log` option, defaulted by
the window and therefore **visible in the shown command line, so the terminal does the same**.
Three exits: back to the step owning the failing setting, open the folder, or continue from the
parts list — which rewrites steps 1 and 2 and drops the user on step 5.

**Windows-only** — step 1's radio stays present and selectable, and `Next` leads to a **step 1a —
Read it on Windows**: the picker and page range still work (page detection needs no recogniser),
the extract command is shown full width with **Copy**, and two buttons re-enter the flow as a
parts list or a set number. "The window is not pretending the option is absent, and it is not
letting the user walk into a failure at minute two. It is handing over the exact line that does
the job on the machine that can."

**New keys** — 15 of its own plus 8 shared with Guard Rail. Revives `UiNext`, `UiBack`,
`UiProgress`, `UiCopyCommand`, `UiCopied`, `UiShowLog`, `UiHideLog`, `UiSettings`,
`ErrOcrUnavailable`.

---

### C. The Run Is The Document

`RunLayout` already says it: *"One folder per run… Everything from a run is then in one place:
easy to look through, easy to archive, easy to delete."* The window does not believe this. It
treats a run as an event that happens once and evaporates. **Make the run the noun the window is
about.**

1. Opens on **Runs** — a list, newest first: name, when, source, status, counts, folder. This is
   where the Catalogue's dead-tab problem goes: the Catalogue is no longer a top-level place, so
   it cannot be empty at the top level.
2. **New run** → **Setup**, one screen: today's Input at the top, one options list below.
   `Start` lives here and only here, so `Problems()` has exactly one place to be.
3. The options list is **one column of 22 rows** with three tools over it: a **search** across
   flag, label and help text; a **changed-only** view driven by what `ToCommandLine` already
   computes, so it costs nothing; and a per-row **reset**. From the second run onward,
   changed-only is the default.
4. `Start` writes `run.json` into the folder **before** the pipeline begins, so the row appears
   immediately and the settings survive a crash.
5. **Reopening a run from the list rebuilds its page from disk** — parts list, `run.json`,
   `stl/`, `3mf/` — with no pipeline run. A run finished last Tuesday is as browsable as one
   finished a minute ago.
6. Rows whose folder has moved are greyed and offer only *forget*. "The index is a convenience;
   the folder is the truth."

**Fixes** — everything is remembered, in the folder that already existed for it. The Catalogue is
never dead. `Start` has one home. `Unverified` becomes a status a run *has*, not a moment the
window mislabels. And *"did I already print this set?"* — which today has no answer anywhere in
the window — gets one.

**Costs — the most expensive, and the only one with a genuine Core obligation, all stated
plainly.** `run.json` must be written by `PipelineRunner`, not the GUI, "or the two front ends
drift and the parity promise rots: a run started in the terminal would not appear in the window's
list."

The sharp cost, in the agent's own framing: `HasOpenEdges` and `HasThinFeatures` are computed
from a live `PreparedMesh` via `ClearanceOffset.ThinnestSpan`, which exists **only in memory
during a run**. Reopening from disk cannot recompute them without re-analysing every `.stl`.
Hence recording them — which means **a folder written by an older version reopens with a
catalogue and no warnings.** "That is a real regression for existing folders and I will not dress
it up": the window must say so rather than showing an unmarked catalogue that looks clean.

It also names its own contradiction: a run index is a fourth thing remembered between sessions,
against a `UserSettings` comment that argues deliberately for remembering little. "I think a run
history is worth it and a scroll position is not — but that comment was written on purpose, and
this proposal contradicts it."

And a testing trap: `Every_option_...` still passes **only if** the search filters by
`IsVisible` rather than re-materialising the list — "or that test starts failing for a reason
nobody will guess."

**Worse off** — throwaway experiments accumulate a list to prune, and the index records paths in
the user's home directory. So: *forget* on every row, *forget everything*, and **no `--api-key`
in the index** — `ToCommandLine` already substitutes `<your key>` and `run.json` must too, "or a
secret ends up in a plain file on disk."

**A run that fails halfway — better here than anywhere**, because the run object outlives the
failure. `run.json` exists from before the pipeline started; the log is written by the pipeline
itself, so it survives a crash of the *window*, not merely a failed run. The row records the
stage it reached: **"stopped while building shapes, 38 of 91" is a thing the window can now say
and today cannot.**

**Windows-only** — the round trip made real. **Runs** gains **Open a run folder**, which adopts a
folder produced elsewhere — the one carried back from the Windows machine — and adds it to the
list as a run you did not start. "The Linux user's flow becomes: set up here, run the extract
there, bring the folder back, continue here. Two machines, one run folder, one list."

**The ~20 options** — nothing deleted, nothing on a second screen, no presets. "The bet is that
the problem was never how many options there are… but that today's column gives no way to find
one or to see which ones you touched." The cost it names itself: an option set three runs ago
stays out of sight under changed-only, so that toggle must **carry the count of hidden rows**.

**New keys** — 15 of its own plus 9 shared. Revives the most dead keys of the three, including
`UiTitle` and `UiSettings`.


---

## Part 3 — Where they disagree, and where a proposal breaks a rule

Stated here rather than fixed quietly. A proposal chosen on a false premise costs far more than
one rejected on a true one.

### The two agents found the same fault from opposite sides

Neither could see the other's report. Both arrived at the same structural fact about today's
`Panel` of four always-alive views, and each saw only half of what it means:

- The **UI designer**, proposing a `TransitioningContentControl` (The Rail) and a `Carousel`
  (The Conveyor): swapping the `Panel` for a control that realises one view at a time **resets
  the log's and the catalogue's scroll positions** on every switch. Cache the views, or leaving a
  run mid-build and coming back scrolls the log to the top.
- The **UX architect**, warning about the same swap: unselected content is never realised, so
  `GetLogicalDescendants` finds nothing and
  `Every_option_the_command_line_takes_is_named_on_the_options_screen` **fails**.

Put together they are worse than either alone: **The Rail and The Conveyor both cost the scroll
positions and the parity test**, and the fix — caching four singleton views in the `ViewLocator`
— has to be in place before either can be built, not after. That is the single most useful thing
to come out of running the two agents in parallel.

### They also converged, unprompted, on two changes

Both, independently, want the OCR check added to `RunSettings.Problems()` so `Start` is disabled
with a reason instead of the run failing part-way at `RunStage.ReadingDocument`. Since `Problems()`
is Core, **the command line inherits the same early refusal.** Every one of the seven proposals
depends on this, so it is not really a choice — it is a prerequisite.

Both also note that `Problems()` returns **hardcoded English strings** today. Any proposal that
puts problems in front of the user has to move those to `TextKey`s first, or an Italian user is
shown English at the one moment the window is telling them something went wrong.

### The two lists are not freely combinable

Step 5 asks for a layout, a colour scheme and a flow as three separate choices. Two of those
three are genuinely independent — **any colour scheme can be applied to any layout**. The layout
and the flow are not:

| | A. Guard Rail | B. The Wizard | C. The Run Is The Document |
|---|---|---|---|
| **1. The Bench** | Works — the expanders are the four screens in pipeline order | **Conflicts** — a wizard gates; the Bench shows everything at once | Partly — no home for a runs list |
| **2. The Rail** | Works — the rail row carries Guard Rail's problem badge | Weak — a rail is not a sequence | **Best fit** — *Runs* becomes a rail item |
| **3. The Conveyor** | Over-built — a ribbon for a flow that is not a sequence | **Same proposal, two halves** | Conflicts — a list of past runs is not a step |
| **4. The Console** | Conflicts — Guard Rail keeps four screens; the Console deletes `Screen` | Conflicts outright | Conflicts — the Console has no places to navigate to |

Two things follow.

**The Conveyor and The Wizard are one proposal seen twice.** The UI agent designed the step
ribbon; the UX architect designed the step machine underneath it. Choosing both is choosing one
coherent thing. Choosing either alone leaves half of it undesigned.

**The Console is a UX proposal wearing UI clothes.** It deletes `Screen`, `ShowCommand` and the
four `OnX` properties — it changes the flow more than Guard Rail does. It is not compatible with
any of the three UX proposals as written, and if it is chosen it *is* the flow choice. The UX
architect did not propose it, so nobody has costed its failure path, its `Unverified` handling,
or its retry — the three things all its UX counterparts spend most of their length on.

### Constraint breaks, named

| Proposal | Rule | What actually happens |
|---|---|---|
| **The Bench**, **The Console** | *No new dependencies without saying so* | Both propose `ItemsRepeater` + `UniformGridLayout` for a virtualised card grid, and both state that no new package is needed. **Verified false:** `UniformGridLayout` is in none of the 23 Avalonia assemblies this build produces, and no `Avalonia.Controls.ItemsRepeater` package is restored. Either keep `ItemsControl` + `WrapPanel` and give up virtualisation, or the user is being asked for a package without being told. |
| **The Bench** | *Confirm each pair clears 4.5:1* | Its light warning text is 4.24:1 on the band it sits on. Fixable with `#8A5300` (5.36:1). Every other pair in all four schemes passes. |
| **The Console** | *Desktop, min 820×560* | Two panes of 380 and 436 px are unusable at 820. It needs an overlay `SplitView` below ~1000 px to meet the minimum at all — the proposal says so itself. |
| **The Console** | *Say the Windows-only thing gracefully* | The remedy moves into a `ToolTip` on a **disabled** control: not reliably reachable across Avalonia backends and **not reachable by keyboard at all**. The proposal names this and says to prefer The Conveyor if it matters. It does. |
| **A. Guard Rail** | *Conservative* | Contains a Core change to `RunSettings.Problems()` that alters command-line behaviour. Declared, not hidden — but it means no option here is purely a window change. |
| **B. The Wizard** | *Do not break what works* | `Every_option_the_command_line_takes_is_named_on_the_options_screen` **fails as written** once options live on three steps. It must be reworked to walk every step. |
| **C. The Run Is The Document** | *No new dependencies* — kept; but | It obliges Core to write a `run.json`, adds a fourth remembered thing against a `UserSettings` comment arguing for remembering little, and **a run folder written by today's build reopens with a catalogue and no warnings**, because the two warnings are computed from a mesh that only exists in memory. All three are declared. |

### What neither agent addressed

- **The application icon is still Avalonia's default `avalonia-logo.ico`.** Four complete visual
  identities were proposed and not one mentions it. Whichever is chosen, the window will still
  announce itself in the task bar as a stock Avalonia application.
- **No in-app theme switch.** All four keep `RequestedThemeVariant="Default"` and let the OS
  decide, which is defensible — but The Console is explicitly *dark-first* while having no way to
  ask for dark, so a Windows user on the default light theme never sees the scheme as designed.
- **`ReviewDirectory` and `OverridesPath`** — the review flow that `RunLayout` was clearly built
  for, and which nothing has ever written to. Proposal C gets closest by making the run folder
  the subject, but none of the seven proposes the review screen those two paths were reserved
  for. That is the largest piece of designed-but-unbuilt behaviour in the program, and it is
  still unclaimed.


---

## Part 4 — What was chosen

Decided by the user on 2026-08-26, from the four layouts, the four palettes and the three flows
above.

| | Chosen |
|---|---|
| **Layout** | **The Rail** — a `SplitView` with a five-item rail, selection as the state |
| **Palette** | **Cool neutral** — accent `#0B6BCB` / `#6BA6F5`, log dark in both themes, no uppercase |
| **Flow** | **The Run Is The Document** — the run folder becomes the subject the window is about |

This is the one pairing the compatibility matrix in Part 3 rates **best fit**: the rail has a
natural home for a *Runs* list as a sixth item, and *Settings* — already the rail's fifth item —
is where `--api-key`, `--log` and the language go once the option list is rebuilt around search
and a changed-only view.

**Carried forward into the implementation plan, and not optional:**

1. **Cache the four view instances in the `ViewLocator` before the `SplitView` lands.** The Rail
   replaces the always-alive `Panel` with a control that realises one view at a time. Part 3
   records why this must come first: it costs the log's and the catalogue's scroll positions,
   **and** it breaks `Every_option_the_command_line_takes_is_named_on_the_options_screen`,
   because `GetLogicalDescendants` finds nothing in an unrealised view.
2. **Add the recogniser check to `RunSettings.Problems()`**, and give `Problems()` its resource
   keys — it returns hardcoded English today.
3. **`run.json` is written by `PipelineRunner`, not by the GUI**, or a run started in the
   terminal never appears in the window's list and the parity promise rots.
4. **`run.json` must mask the API key** the way `ToCommandLine` already does, or a secret lands
   in a plain file on disk.
5. **A run folder written by today's build reopens with a catalogue and no warnings**, because
   `HasOpenEdges` and `HasThinFeatures` are computed from a mesh that exists only in memory. The
   window has to say so rather than showing an unmarked catalogue that looks clean.
6. The palette's one correction is not needed here — the failing warning colour was The Bench's,
   not this one. Every pair in Cool neutral clears 4.5:1, with a floor of 4.87.

Two things neither agent claimed and which remain open: the application icon is still Avalonia's
default, and `RunLayout.ReviewDirectory` / `OverridesPath` are still unclaimed by any proposal.

**Implementation is a separate plan, and it starts with brainstorming — not with editing
`.axaml`.**
