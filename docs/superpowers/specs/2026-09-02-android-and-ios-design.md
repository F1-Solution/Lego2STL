# Android And iOS — Design

**Date:** 2026-09-02
**Status:** design approved 2026-09-02. Sub-project E of
`2026-08-31-print-quality-and-mobile-roadmap.md`, the last of the four and the one the whole
decomposition was originally asked for.
**Depends on:** D, which put a real recogniser on Android and iOS behind the `IOcrEngine` seam,
and on A to C, which are settled and are not re-opened here.

---

## What is already true

More works on a phone than the request assumed. `PDFtoImage` already targets
`net10.0-android36.0` and `net10.0-ios26.0` and carries the right PDFium and SkiaSharp natives, so
pages render. D put `AndroidOcrEngine` and `AppleOcrEngine` behind `OcrEngines.Create`, so a
scanned book can be read. `OpenScadRunner`, the one thing in the repository that launches an
external process, belongs to the command line's `bricks` command and never to the pipeline.

So reading a document, building a parts list, generating shapes and packing plates are all already
possible on a phone. What is missing is an application to do them in, somewhere to put what comes
out, and a way to get it back off the device.

## What this builds

Four things, in the order they depend on each other: the project grows two more targets, the
window becomes a view, a run gets a home that is not "beside the input", and the geometry source
learns to stop before the 80 MB download. Then CI proves it.

---

## 1. The project shape

`Lego2STL.Gui.csproj` gains `net10.0-android36.0` and `net10.0-ios26.0` alongside today's
`net10.0;net10.0-windows10.0.19041.0`. One project, four targets, with per-platform folders
included by condition — exactly the pattern `Lego2STL.OcrSmokeTest` already uses and D already
proved builds. There is no separate `Lego2STL.Gui.Android` head project, and no shared-library
split: views, view models, `ViewLocator`, `Localization` and `Services` all stay where they are,
and the desktop application keeps its own file paths unchanged.

`Avalonia.Android` and `Avalonia.iOS` exist at **12.1.1**, the same version as the four Avalonia
packages already referenced, so no version split is introduced. They are referenced per target, as
`Avalonia.Desktop` becomes conditioned on the two desktop targets.

Entry points, one per platform, under `Platforms/`:

| Target | Entry point | Excluded from other targets by |
|---|---|---|
| desktop | `Program.cs`, today's `[STAThread] Main` | `Compile Remove` on the mobile targets |
| Android | `Platforms/Android/MainActivity.cs`, `AvaloniaMainActivity<App>` | `'$(IsAndroidTarget)' != 'true'` |
| iOS | `Platforms/iOS/AppDelegate.cs` and `Main.cs`, `AvaloniaAppDelegate<App>` | `!$(TargetFramework.Contains('-ios'))` |

Two constraints D discovered apply again here and are not to be rediscovered: the Android
manifest's `minSdkVersion` and the csproj's `SupportedOSPlatformVersion` must agree exactly or the
SDK refuses with `XA1036`, and an `Info.plist` that does not sit at the project root is silently
ignored unless `<InfoPlist>` names it. `OutputType` stays `WinExe` for the desktop targets and
becomes `Exe` on the mobile ones.

**macOS is not a GUI target.** Avalonia's desktop target already runs on macOS through `net10.0`;
`net10.0-macos26.0` exists in Core only because the Vision recogniser needs it.

## 2. The window becomes a view

Everything inside `MainWindow.axaml` moves into a new `Views/MainView.axaml`, a `UserControl`.
`MainWindow` stays, reduced to a shell that hosts `MainView` and keeps what only a window has: the
1040x720 size, the 820x560 minimum, the title and the icon.

`App.OnFrameworkInitializationCompleted` then serves both lifetimes from one `MainViewModel`:

- `IClassicDesktopStyleApplicationLifetime` — a `MainWindow` wrapping the view, as today,
  including the `ShutdownRequested` disposal that already exists.
- `ISingleViewApplicationLifetime` — `MainView` alone, assigned to its `MainView` property.

This is the only structural change to existing XAML, and it is what makes phone parity a fact
about one layout rather than a promise about two.

**The layout adapts rather than forking.** The sidebar is a `Border` docked left at a fixed 200 px.
Below a width threshold it collapses into a flyout reached from the top bar, driven by a single
`IsCompact` property on `MainViewModel` bound from the view's own bounds — not by a platform
check, so a narrow desktop window behaves the same way and the state is testable off-screen
without a device. The catalogue's 264 px cards already reflow to one per row and need nothing.

## 3. Where a run lives, and how it gets out

**The seam.** `RunLayout.For` computes a run folder from the input's own path — "beside the
input". On Android and iOS the input arrives from a document picker as a stream, the application is
sandboxed, and there is no such place. Core gains `IRunHome`, mirroring `IOcrEngine`: the
implementation that ships everywhere is today's behaviour, and the mobile heads supply one backed
by application storage.

The mobile implementation does one thing before deferring to the existing logic: it **copies the
picked document into application storage first**, and then the run happens beside that copy. So
`RunLayout` itself gains no phone-specific branch, a run folder on a phone has the same shape as a
run folder on a desktop, and the runs list — which reads folders — works unchanged. The cost is a
second copy of the input on the device, which is honest and is the user's to delete.

**Getting results out.** `Desktop.Open` and `Desktop.Reveal` are called from nine places across
`CataloguePartViewModel`, `RunDocumentViewModel` and `SettingsViewModel`. They become a Gui-side
seam with three implementations: today's `Process.Start` on desktop, `ACTION_SEND` on Android, and
`UIActivityViewController` on iOS. Revealing a folder has no phone equivalent, so on mobile the
folder button shares the run's parts list instead, and the shape and plate buttons share the file.
This stays in Gui and does not enter Core: it is a UI act, not a pipeline one. `Desktop.Open` on a
web address keeps working on all three, since every platform opens a URL.

## 4. LDraw on a phone

`LDrawSourceOptions` already escalates local directory, then per-file HTTP, then `complete.zip`
after `RefusalsBeforeFullDownload` misses. It gains `AllowFullArchive`, defaulting **true** so
desktop and the command line are unchanged, which the mobile heads set **false**.

A capped run downloads only the `.dat` files its own parts list needs, into the cache directory
under application storage. Parts that cannot be resolved that way are reported by name through
`EscalatingLDrawLibrary.Missing`, which already exists and is already surfaced. No 80 MB download
ever happens on a phone, and none is silently started over a mobile connection.

A user who wants the whole library on a device can still point `LocalDirectory` at a folder they
supplied themselves; nothing forbids it.

## 5. What CI proves

The existing `mobile` job builds Core for three targets. It extends to build the Gui heads too: an
Android **APK**, and an iOS build **for the simulator**. There is no Apple Developer Program
membership, so there is no certificate, no provisioning profile and nothing installable on a
device; the signing step is **written and switched off**, so it becomes a matter of adding secrets
rather than a matter of writing a job.

Then the part that is new to this repository: a run is proved by machine, not only by hand.

**A self-driving smoke head**, `tests/Lego2STL.MobileSmokeTest`, built the way
`Lego2STL.OcrSmokeTest` already is — the same `$(MobileTargetFrameworks)`, the same per-platform
`Compile Remove`, the same PASS/FAIL contract. It carries a small **typeset** fixture, takes it
through the real pipeline under the real `IRunHome` and the real capped LDraw source, and reports
whether a parts list and a plate came out. Typeset, so it needs no recogniser and no network: it
proves the phone *pipeline*, and D's smoke test remains what proves the phone *recogniser*.

CI launches it twice:

| Platform | How | Verdict read from |
|---|---|---|
| Android | boot an emulator, install, launch | `adb logcat` |
| iOS | boot a simulator, install, launch | `xcrun simctl launch --console` |

**Why a head and not UI automation.** Driving the real screens would prove more, but it needs an
automation stack this repository has not got — Appium, or Espresso and XCUITest — and taps on an
emulator are the classic source of a slow, flaky job. The head cannot go flaky over a mis-timed
tap, and it extends a pattern that is already here and already worked.

**What is still a person's job.** Everything about the application as a thing to use: whether the
collapsed sidebar is usable in the hand, whether the document picker offers the right files,
whether the share sheet lands the plate where it should. Off-screen renders of `MainView` at phone
metrics in `Lego2STL.UiTests` cover the layout state; they do not cover the feel of it, and saying
otherwise would be a lie the earlier phases did not tell either.

## 6. Not in scope

- **The `bricks` command.** It launches OpenSCAD, which does not exist on a phone. It is CLI-only
  today and stays so.
- **Linux OCR.** D settled it: deliberately uncovered.
- **Anything needing an Apple Developer membership** — device builds, TestFlight, the store.
- **A second layout for phones.** Rejected in favour of the `MainView` split, so that parity keeps
  costing one change rather than two.
- **Elephant-foot compensation, orientation rules, tolerance presets.** A to C settled these, and E
  does not re-litigate them.

## 7. Risks, and what would change the design

| Risk | What it would mean |
|---|---|
| Avalonia 12.1.1's Android or iOS backend behaves differently from the desktop one on some control the screens use | Fix that control, not fork the layout. Found by the first emulator run, which is the argument for building the smoke head early rather than last. |
| The workload and restore trap D hit — a missing workload failing restore for every project referencing the changed one | Now applies to `Lego2STL.Gui` as well as Core, so `Cli`, `Tests` and `UiTests` all feel it. The mitigation is D's: `-p:TargetFrameworks=` on every packaging call, and `-f` alongside it for `publish`. |
| An emulator job slower or flakier than hoped | It is one launch and one log line, not a tap sequence. If it still proves unreliable it moves to a nightly rather than growing retries. |
| A run on a phone wanting more memory than a phone has | The reference run is 175 shapes; nothing here changes the pipeline's memory behaviour. Measured on the first real device run, not designed around in advance. |

## 8. Settled, so it is not asked again

The three questions the roadmap left open for this spec:

1. **How far the project structure moves** — one multi-targeted project, no head projects, no
   shared-library split. The only file whose content moves is `MainWindow.axaml`.
2. **Whether storage is a seam in Core or a service the heads provide** — a seam in Core,
   `IRunHome`, because `RunLayout` is Core's and the command line has to keep working unchanged.
3. **What a phone does about an 80 MB library** — never downloads it; `AllowFullArchive` is false
   on mobile and per-part fetch is the whole strategy.
