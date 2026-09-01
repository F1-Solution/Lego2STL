# D — A Recogniser On Every Platform

**Date:** 2026-09-01
**Status:** design approved 2026-09-01, not yet planned or built.
**Comes from:** `2026-08-31-print-quality-and-mobile-roadmap.md`, sub-project D.

---

## What is wrong now

Reading a page that carries no text needs a recogniser. `IOcrEngine` is the seam — `OcrEngines.Create()` is the single place that knows which platform is running, and everything downstream reads text through the interface without naming an implementation. Today that seam has exactly one implementation, `WindowsOcrEngine`, wrapping `Windows.Media.Ocr`. Off Windows, `OcrEngines.Create()` throws `OcrUnavailableException` and says so.

That leaves Linux, macOS, Android and iOS unable to read a scanned instruction book. A *typeset* book is unaffected — `ReadPrintedCatalogue` takes element numbers straight out of the page's own text and needs no recogniser at all, which is why this tool has already been useful on Linux against the reference document `6324712.pdf`. It is the scanned case, `6324096.pdf`-style, that needs this phase.

## The approach

One native recogniser per new platform, behind the interface that already exists, each shaped exactly like `WindowsOcrEngine`: a sealed class, a `Create` factory, a `ReadAsync` that bridges the platform's native bitmap type and returns joined-by-newline recognised text.

| Platform | Engine | API | Package |
|---|---|---|---|
| Android | `AndroidOcrEngine` | ML Kit on-device text recognition | `Xamarin.Google.MLKit.TextRecognition` + `Xamarin.Google.MLKit.TextRecognition.Bundled.Common` (Microsoft-maintained, MIT) |
| iOS | `AppleOcrEngine` | `Vision.VNRecognizeTextRequest` | none — built into the .NET for iOS bindings |
| macOS | `AppleOcrEngine` (same class, second target) | `Vision.VNRecognizeTextRequest` | none — built into the .NET for macOS bindings |

**Why the bundled ML Kit model.** `Xamarin.Google.MLKit.TextRecognition.Bundled.Common` ships the recognition model inside the app rather than downloading it from Play Services on first use. `WindowsOcrEngine`'s own reasoning — "needs nothing installed, downloads no models" — applies here for the same reason: a run should not depend on network access or Play Services being present to read a page.

**Why one `AppleOcrEngine`, not two.** `Vision.VNRecognizeTextRequest` is the identical API on iOS and macOS. The roadmap's own problem statement names macOS as broken today, alongside Linux — the "approach" section names iOS explicitly and doesn't rule macOS out, and closing it costs one more target framework and no new design, so it is in scope. Linux gets no engine and no branch, deliberately: nothing in this design gives it one, matching the roadmap's explicit call.

**Why not Tesseract or an ONNX model.** Already settled by the roadmap document and not reopened here: Tesseract ships no usable native runtime across all four platforms, and an ONNX model would mean choosing, shipping, and training detection ourselves. Neither beats a first-party recogniser per platform.

## Wiring

`OcrEngines` grows two more conditional branches next to the existing `#if WINDOWS` one:

```csharp
#if WINDOWS
    return WindowsOcrEngine.Create(languageTag);
#elif ANDROID
    return AndroidOcrEngine.Create(languageTag);
#elif IOS || MACOS
    return AppleOcrEngine.Create(languageTag);
#else
    throw new OcrUnavailableException(DescribeUnavailable(words));
#endif
```

`IsAvailable` and `DescribeUnavailable` extend the same way — a Linux build is still told plainly that there is no recogniser there, and why.

## Build

`Directory.Build.props`'s `TargetFrameworks` grows from two to five:

```
net10.0;net10.0-windows10.0.19041.0;net10.0-android36.0;net10.0-ios26.0;net10.0-macos26.0
```

(Version numbers pinned to whatever .NET 10's mobile workload actually ships, the same way the Windows target's `10.0.19041.0` is pinned today — verified against the installed SDK when this is built, not assumed here.)

`Directory.Build.props` names the new targets the same way it already names `IsWindowsTarget`, so conditions elsewhere read clearly:

```
IsAndroidTarget, IsApplePlatformTarget (true for both -ios and -macos)
```

`Lego2STL.Core.csproj` gets two more `Compile Remove` groups next to the existing Windows one — `AndroidOcrEngine.cs` compiles only into the Android target, `AppleOcrEngine.cs` only into the two Apple targets. The ML Kit package references go in an `ItemGroup` conditioned on `IsAndroidTarget`, matching how `SkiaSharp` and `PdfPig` are unconditional today and the (future) Windows-only packages would be conditioned on `IsWindowsTarget`.

No other project changes. `Lego2STL.Cli` and `Lego2STL.Gui` are untouched — there is no mobile head to wire this into yet (Phase E's job); Phase D lands `Core` able to recognise text on every platform with nothing yet calling it there.

## Testing

Two tiers, matching how much of this can actually be proven by a machine versus a person, and following the existing `DocumentFactAttribute` idiom of "skip with a stated reason" wherever real recognition needs something CI cannot supply.

**1. CI — build verification, every target, every push.** `.github/workflows/package.yml` installs the Android and iOS/Mac Catalyst workloads and adds `Lego2STL.Core` to the build matrix for all five target frameworks. This is new infrastructure — no job today builds anything but the existing two targets — and its job is narrow: catch API misuse against the ML Kit and Vision bindings at compile time. It does not boot an emulator or a simulator, and it does not call the real recogniser. The existing engine-agnostic pipeline tests (`LabelReader`, `CatalogueReader`, and everything that consumes `IOcrEngine` through a fake) keep passing unchanged — they were never platform-specific and stay that way.

**2. Device — a runnable smoke test, triggered by a person.** A new project, `tests/Lego2STL.OcrSmokeTest`, targeting `net10.0-android`, `net10.0-ios` and `net10.0-macos`. Its job is narrower than the existing accuracy gate (`LabelReadingAccuracyTests`, which stays exactly as it is — Windows-only, gated on the real reference document being present): prove each binding is wired correctly and returns real recognised text, not re-prove OCR accuracy against a genuine catalogue page.

Its fixture is a small image rendered in-process with SkiaSharp at test time — text such as `"32523, 11"` drawn with a white margin, in the same shape `RowCrop` produces for the real pipeline — committed to the repo as generating code, not as a binary asset, and carrying no dependency on the undistributed reference PDF.

Each platform gets the thinnest host that can show a result:

- **Android** — one `Activity` that calls `AndroidOcrEngine.Create()`, reads the fixture, and displays PASS/FAIL plus the recognised text.
- **iOS** — one single-view app doing the same, run in the Simulator (Vision works in the Simulator; nothing here needs a physical device).
- **macOS** — no UI at all. Vision runs headless, so this is a plain console app: `dotnet run -f net10.0-macos` prints PASS/FAIL to stdout.

A person launches each — emulator, Simulator, or the Mac itself — before a release that claims mobile OCR works, and reads the result off the screen or the console. There is no way to detect "an emulator happens to be running" the way `ReferenceDocument.TryFind()` detects a file, so this tier stays manually triggered rather than auto-skipping; what CI cannot supply, a person supplies, once, deliberately, per release.

## Error handling

Both new engines propagate exceptions exactly the way `WindowsOcrEngine` does today — nothing here invents retry logic or swallows a platform failure. `OcrUnavailableException` keeps its one meaning: *this build has no recogniser at all* (Linux only, after this phase). A recogniser that is present but fails or returns nothing on a given image is the pre-existing, already-handled case — an empty string is "a normal outcome, not an error," per `IOcrEngine`'s own contract.

## Non-goals

- **No mobile UI.** `Lego2STL.Cli` and `Lego2STL.Gui` are not touched. Giving a phone something to call this from is Phase E.
- **No Linux recogniser.** Deliberate, per the roadmap; nothing in this design changes it.
- **No network-based ML Kit model download path.** The bundled model is chosen specifically to avoid needing one.
- **No shared base class across the three new engines beyond what already shares.** Each stays as flat as `WindowsOcrEngine` — a class, a factory, a `ReadAsync`. `AppleOcrEngine` compiling into two targets is sharing through the compiler, not through an inheritance hierarchy invented for this.
- **No attempt to make the smoke test project's fixture stand in for the real accuracy gate.** `LabelReadingAccuracyTests` remains the only place accuracy against a genuine page is measured, and it stays Windows-and-reference-document-gated exactly as it is today.

## Open items for the plan

- The exact `net10.0-android`, `net10.0-ios` and `net10.0-macos` version suffixes, pinned against whatever SDK the workload install actually resolves to.
- Whether GitHub-hosted runners can install the Android/iOS/Mac Catalyst workloads within the existing job time budget, or whether this needs its own job separate from `windows`/`linux`/`macos` in `package.yml`.
- The exact ML Kit package versions and whether the `.Bundled.Common` variant needs a companion `ProGuard`/linker configuration for a self-contained Android build (Phase E's concern once a real Android head exists, but worth confirming Core alone builds clean without one).
