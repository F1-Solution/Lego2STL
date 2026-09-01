# A Recogniser On Every Platform — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `IOcrEngine` gains a working implementation on Android, iOS and macOS, so `OcrEngines.Create()`
returns a real recogniser everywhere except Linux, exactly as it already does on Windows.

**Architecture:** Two new sealed classes in `Lego2STL.Core.Ocr` — `AndroidOcrEngine` (ML Kit, bundled
model) and `AppleOcrEngine` (Vision, one class compiled into both the iOS and macOS targets) — shaped
identically to the existing `WindowsOcrEngine`: a factory, a `Name`, a `ReadAsync` that bridges the
platform's native bitmap type. `Lego2STL.Core` alone grows three new target frameworks; `Lego2STL.Cli`
and `Lego2STL.Gui` are untouched, since nothing consumes these engines from a UI yet. A new CI job
builds all three new targets without booting an emulator or a simulator. A new, separate test project
gives a person a repeatable way to check real recognition by hand on a device.

**Tech Stack:** C# / .NET 10, SkiaSharp 4.150.1, Xamarin.Google.MLKit.TextRecognition (Android),
Vision (iOS/macOS, part of the platform bindings), dotnet-android / dotnet-ios / dotnet-maccatalyst
workloads.

**Spec:** `docs/superpowers/specs/2026-09-01-a-recogniser-on-every-platform-design.md`

## Global Constraints

- Build with `dotnet build Lego2STL.slnx -c Debug` (no `-f`). Test with `dotnet test Lego2STL.slnx`.
  **Never pass `-f` at the solution level** — `Lego2STL.Tests` and `Lego2STL.UiTests` only ever
  target `net10.0-windows10.0.19041.0` (they are not multi-targeted), so a solution-wide `-f`
  forces a framework they do not have and fails with a confusing missing-symbol error, unrelated to
  anything this plan changes. Scope a `-f` or `-p:TargetFrameworks=` override to one project
  (`dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -f <tfm>`) when a single target needs
  checking in isolation, and run a fresh `dotnet restore Lego2STL.slnx` (or delete `obj`/`bin`)
  afterward — a scoped override leaves a stale `project.assets.json` behind that the next
  unscoped build silently reuses.
- **This machine started with none of the mobile workloads installed, and that broke more than the
  three new targets.** The moment `Lego2STL.Core.csproj` lists a `TargetFramework` whose workload is
  missing, **restore fails for every project that references Core** — `Lego2STL.Cli`, `Lego2STL.Gui`,
  `Lego2STL.Tests`, `Lego2STL.UiTests`, and Core itself — regardless of which single framework `-f`
  asks to build, because the SDK validates workload requirements for every listed `TargetFramework`
  of every project in the graph during restore, not just the one selected. So Task 1 installed
  `android`, `ios`, `maccatalyst` and `macos` on this machine (`dotnet workload install android ios
  maccatalyst macos` — took roughly 90 minutes end to end, several GB, confirmed with the user
  first) rather than working around it. With the workloads present, **every task in this plan can be
  built and, where a test project exists, tested locally** — the earlier assumption that Tasks 3, 4,
  7, 8 and 9 could only be compiled in CI no longer holds. What still cannot be done here is running
  the smoke test project for real (Task 7 onward) — that needs a booted emulator, simulator or a
  Mac, none of which this machine has — and CI (Task 6) remains the only place proving the exact
  workload versions CI's own runners resolve.
- **A pre-existing failure, unrelated to this plan: `Lego2STL.UiTests.CalibrationPlateTests
  .With_nothing_to_build_from_it_says_so_and_writes_no_shapes` fails on unmodified `main`.**
  Confirmed by stashing every change from this plan and re-running the test in isolation before
  Task 1 began — it failed identically. It sometimes drags a second `CalibrationPlateTests` case
  down with it when the whole suite runs together (2 failures instead of 1), which looks like
  shared-state flakiness on top of the underlying failure, not something this plan caused. Treat the
  baseline as "`Lego2STL.Tests` 653/653, `Lego2STL.UiTests` all green except this one test (and
  occasionally a second `CalibrationPlateTests` case)" — not "all green" — when judging whether a
  task regressed anything. Do not fix it as part of this plan; it is out of Phase D's scope.
- **`act` cannot run this plan's new CI job.** It needs Xcode for the iOS/macOS legs, and act runs
  Linux containers only — the same limitation already recorded for the `windows` and `macos` jobs in
  `README-act.md`. There is no local route to verifying it; the first real signal is the job running
  on `macos-latest` after this is pushed.
- **Binding namespace and method names are written from public documentation, not from a package
  installed anywhere in this repository.** Every task that adds a call into
  `Xamarin.Google.MLKit.TextRecognition` or `Vision` says so and expects the first real build (in CI,
  Task 6) to be where casing or an overload gets corrected. That correction is expected engineering
  work against a binding this repository has never used before, not a sign the task was done wrong.
- Directory.Build.props's shared `TargetFrameworks` is **not** where the three new targets go — that
  property is inherited by `Lego2STL.Cli` and `Lego2STL.Gui` too, and neither is ready to build for a
  mobile platform (that is Phase E). The three new targets are added only inside
  `Lego2STL.Core.csproj`, by appending a new `MobileTargetFrameworks` property that
  `Directory.Build.props` defines but does not itself build.
- Source files are CRLF. They carry a UTF-8 byte-order mark **only when they contain a non-ASCII
  character** — this repository's actual convention, not a rule to reapply everywhere.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal class or
  method names.
- Files stay under 800 lines; functions under 50.
- After each task append one line to `PROGRESS.md`:
  `PHASE:D WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>`, and `PHASE:D WAVE:0` when all nine are done.

---

### Task 1: The three new target frameworks, scoped to Core alone

**Files:**
- Modify: `Directory.Build.props`
- Modify: `src/Lego2STL.Core/Lego2STL.Core.csproj`

**Interfaces:**
- Consumes: nothing new.
- Produces: MSBuild properties `IsAndroidTarget`, `IsApplePlatformTarget` (both booleans, defined in
  `Directory.Build.props`, readable from every project), and `MobileTargetFrameworks` (a
  semicolon-joined string, also defined in `Directory.Build.props`). `Lego2STL.Core.csproj`'s own
  `TargetFrameworks` grows to five entries; every other project's stays at two.

- [ ] **Step 1: Add the two new conditional properties to `Directory.Build.props`**

Add this `PropertyGroup` right after the existing one labelled `"What distinguishes the two targets,
named once so conditions read clearly"` (keep that one's `IsWindowsTarget` exactly as it is):

```xml
  <PropertyGroup Label="The three targets Phase D adds, and where Core alone opts into them">
    <IsAndroidTarget>false</IsAndroidTarget>
    <IsAndroidTarget Condition="$(TargetFramework.Contains('-android'))">true</IsAndroidTarget>
    <IsApplePlatformTarget>false</IsApplePlatformTarget>
    <IsApplePlatformTarget Condition="$(TargetFramework.Contains('-ios')) OR $(TargetFramework.Contains('-macos'))">true</IsApplePlatformTarget>
    <!--
      A property, not a literal repeated in Core.csproj, so the three version suffixes are
      pinned in exactly one place. Only Lego2STL.Core appends this to its own TargetFrameworks;
      Lego2STL.Cli and Lego2STL.Gui inherit the plain two-target list above unchanged, because
      neither has a mobile head yet - that is Phase E's job, not this one's.
    -->
    <MobileTargetFrameworks>net10.0-android36.0;net10.0-ios26.0;net10.0-macos26.0</MobileTargetFrameworks>
  </PropertyGroup>
```

- [ ] **Step 2: Opt `Lego2STL.Core` into the mobile targets**

Open `src/Lego2STL.Core/Lego2STL.Core.csproj`. It currently has no `TargetFrameworks` element of its
own (it inherits the two-target list from `Directory.Build.props`). Add one, as the very first
element inside the existing `<Project Sdk="Microsoft.NET.Sdk">` — before the first `<ItemGroup>`:

```xml
  <PropertyGroup>
    <!--
      The only project that reads text, so the only one that needs a recogniser for every
      platform. TargetFrameworks already holds the two-target list from Directory.Build.props
      by the time this line runs, because Directory.Build.props is imported before a project's
      own body; appending here adds three more without touching what Cli or Gui inherit.
    -->
    <TargetFrameworks>$(TargetFrameworks);$(MobileTargetFrameworks)</TargetFrameworks>
  </PropertyGroup>
```

- [ ] **Step 3: Confirm the two existing targets still build**

Run: `dotnet build Lego2STL.slnx -c Debug` (no `-f` — see Global Constraints)
Expected: succeeds, all five targets, same as before this task for the two that already existed.

The three new targets cannot be built here — no workload for them is installed on this machine.
That is expected; Task 6 is where they first actually build, in CI.

- [ ] **Step 4: Run the existing suite**

```
dotnet test Lego2STL.slnx
```

Expected: **PASS, unchanged.** Nothing in this task touches code the suite exercises.

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props src/Lego2STL.Core/Lego2STL.Core.csproj
git commit -m "chore: Core alone grows the android, ios and macos target frameworks"
```

---

### Task 2: Package references and per-target file exclusion

**Files:**
- Modify: `src/Lego2STL.Core/Lego2STL.Core.csproj`

**Interfaces:**
- Consumes: `IsAndroidTarget`, `IsApplePlatformTarget` from Task 1.
- Produces: two files that do not exist until Task 3 and Task 4 — `Ocr\AndroidOcrEngine.cs` and
  `Ocr\AppleOcrEngine.cs` — are declared here as excluded everywhere except their own target, the
  same way `Ocr\WindowsOcrEngine.cs` already is.

- [ ] **Step 1: Add the two new `Compile Remove` groups next to the existing Windows one**

In `src/Lego2STL.Core/Lego2STL.Core.csproj`, right after:

```xml
  <ItemGroup Condition="'$(IsWindowsTarget)' != 'true'">
    <!-- The only file that talks to Windows. Off the Windows target there is no recogniser. -->
    <Compile Remove="Ocr\WindowsOcrEngine.cs" />
  </ItemGroup>
```

add:

```xml
  <ItemGroup Condition="'$(IsAndroidTarget)' != 'true'">
    <!-- The only file that talks to ML Kit. Off the Android target there is no recogniser. -->
    <Compile Remove="Ocr\AndroidOcrEngine.cs" />
  </ItemGroup>

  <ItemGroup Condition="'$(IsApplePlatformTarget)' != 'true'">
    <!-- The only file that talks to Vision. Off an Apple target there is no recogniser. -->
    <Compile Remove="Ocr\AppleOcrEngine.cs" />
  </ItemGroup>
```

- [ ] **Step 2: Add the ML Kit package references, conditioned on the Android target only**

Add a new `ItemGroup` after the one above:

```xml
  <ItemGroup Condition="'$(IsAndroidTarget)' == 'true'">
    <!--
      The bundled variant, not the Play-Services one: it ships the recognition model inside
      the app instead of downloading it on first use, so a run needs no network access and no
      Play Services to read a page - the same reason WindowsOcrEngine was chosen over anything
      that downloads a model.
    -->
    <PackageReference Include="Xamarin.Google.MLKit.TextRecognition" Version="116.0.1.7" />
    <PackageReference Include="Xamarin.Google.MLKit.TextRecognition.Bundled.Common" Version="117.0.0.7" />
  </ItemGroup>
```

- [ ] **Step 3: Create two empty placeholder-free stub files so the exclusion has something to exclude**

`Compile Remove` on a file that does not exist is silently a no-op, which would hide a typo in the
path. Create both files now, minimal but real, so Task 3 and Task 4 edit rather than create them:

`src/Lego2STL.Core/Ocr/AndroidOcrEngine.cs`:

```csharp
namespace Lego2STL.Core.Ocr;

// Body written in Task 3.
```

`src/Lego2STL.Core/Ocr/AppleOcrEngine.cs`:

```csharp
namespace Lego2STL.Core.Ocr;

// Body written in Task 4.
```

- [ ] **Step 4: Confirm the whole solution still builds and the suite still passes**

Run: `dotnet build Lego2STL.slnx -c Debug` (no `-f` — see Global Constraints)
Run: `dotnet test Lego2STL.slnx`
Expected: both succeed against the baseline recorded in Global Constraints — `Lego2STL.Tests`
653/653, `Lego2STL.UiTests` all green except the pre-existing `CalibrationPlateTests` failure. The
two placeholder files are excluded from every target except their own by the groups just added, so
nothing sees a bare `// Body written in Task N` file trying to compile.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Lego2STL.Core.csproj src/Lego2STL.Core/Ocr/AndroidOcrEngine.cs src/Lego2STL.Core/Ocr/AppleOcrEngine.cs
git commit -m "chore: wire the android and apple recogniser files into the build, bodies to follow"
```

---

### Task 3: `AndroidOcrEngine`

**Files:**
- Modify: `src/Lego2STL.Core/Ocr/AndroidOcrEngine.cs`

**Interfaces:**
- Consumes: `IOcrEngine` (`src/Lego2STL.Core/Ocr/IOcrEngine.cs`), `RowCrop.ToPng(SKBitmap)`
  (`src/Lego2STL.Core/Ocr/RowCrop.cs`).
- Produces:
  - `sealed class AndroidOcrEngine : IOcrEngine`
  - `static AndroidOcrEngine AndroidOcrEngine.Create(string? languageTag = null)`

- [ ] **Step 1: Write the engine**

Replace the contents of `src/Lego2STL.Core/Ocr/AndroidOcrEngine.cs` with:

```csharp
using Android.Gms.Extensions;
using Android.Graphics;
using SkiaSharp;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Text recognition using ML Kit's on-device recogniser.
/// </summary>
/// <remarks>
/// The bundled model, not the Play-Services one: it ships inside the app rather than being
/// downloaded on first use, which is the same reason <see cref="WindowsOcrEngine"/> was
/// chosen over anything that downloads a model - a run should need no network access and no
/// Play Services to read a page.
/// </remarks>
public sealed class AndroidOcrEngine : IOcrEngine
{
    private readonly ITextRecognizer _recognizer;

    private AndroidOcrEngine(ITextRecognizer recognizer, string languageTag)
    {
        _recognizer = recognizer;
        Name = $"ML Kit ({languageTag})";
    }

    public string Name { get; }

    /// <summary>
    /// Creates an engine. ML Kit's Latin recogniser covers the digits and Latin letters the
    /// text here is made of regardless of which language tag is asked for, so the tag is
    /// carried only for <see cref="Name"/> and is not passed to ML Kit itself.
    /// </summary>
    public static AndroidOcrEngine Create(string? languageTag = null)
    {
        var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        return new AndroidOcrEngine(recognizer, languageTag ?? "latin");
    }

    public async Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = ToAndroidBitmap(image);
        var input = InputImage.FromBitmap(bitmap, 0);

        // Process(...) returns a Java Task, bridged to a .NET one so this method can be
        // awaited like every other IOcrEngine implementation. Text is written fully
        // qualified: Vision.Text is both a namespace and, within it, a type name, and the
        // bare name resolves to the namespace.
        var result = await _recognizer.Process(input)
            .AsAsync<Xamarin.Google.MLKit.Vision.Text.Text>()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        // Keep the engine's own line breaks, the same choice WindowsOcrEngine makes: the
        // caller scans the text for the shapes it expects rather than relying on any
        // particular joining.
        return string.Join('\n', result.TextBlocks.Select(b => b.Text)).Trim();
    }

    /// <summary>
    /// Bridges SkiaSharp to Android's bitmap type. Goes via PNG, the same route
    /// <see cref="WindowsOcrEngine"/> uses to bridge to WinRT imaging, for the same reason:
    /// it needs no hand-marshalled pixel buffer and no stride, premultiplication or channel
    /// order to get wrong.
    /// </summary>
    private static Bitmap ToAndroidBitmap(SKBitmap image)
    {
        var png = RowCrop.ToPng(image);
        return BitmapFactory.DecodeByteArray(png, 0, png.Length)
            ?? throw new InvalidOperationException("Android could not decode the cropped row as a bitmap.");
    }
}
```

**The code above is verified against a real build**, not merely documentation, and the namespaces
in it are the ones that actually compile — this is worth recording because the first attempt did
not compile as `Com.Google.MLKit.Vision.Common` / `.Vision.Text`, and finding the real ones took
some digging. `TextRecognition`, `ITextRecognizer`, `Text` and `Latin.TextRecognizerOptions` are
not in the `Xamarin.Google.MLKit.TextRecognition` package's own assembly at all — that assembly
turned out to hold only a "dynamite module descriptor" stub. They live in
`Xamarin.GooglePlayServices.MLKit.Text.Recognition[.Common]`, which is pulled in *transitively* by
the two packages Task 2 already references (`Xamarin.Google.MLKit.TextRecognition` and its
`.Bundled.Common`), so no new `PackageReference` was needed — only the corrected `using`s above.
`AsAsync<T>()` comes from `Android.Gms.Extensions` (in `Xamarin.GooglePlayServices.Tasks`, already
a transitive dependency).

Run: `dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Debug -f net10.0-android36.0`
Expected: succeeds, 0 errors (a handful of pre-existing `CS1574`/`CS1570` doc-comment warnings from
code Task 3 doesn't touch are fine, including two new ones where this file's own `<see
cref="WindowsOcrEngine"/>` can't resolve on a target that excludes that file).

If a future ML Kit package upgrade breaks this again, the fix is the same technique used to find
these names the first time: read the actual type names out of the installed assembly's metadata
(e.g. with `System.Reflection.PortableExecutable.PEReader` and `GetMetadataReader()`, which reads
type and member names without needing to load the assembly's own runtime dependencies) rather than
guessing from documentation. Run `dotnet restore Lego2STL.slnx` afterward before touching any other
project, per the Global Constraints note on scoped overrides leaving a stale `project.assets.json`
behind.

- [ ] **Step 2: Commit**

```bash
git add src/Lego2STL.Core/Ocr/AndroidOcrEngine.cs
git commit -m "feat: add the Android text recogniser"
```

---

### Task 4: `AppleOcrEngine`

**Files:**
- Modify: `src/Lego2STL.Core/Ocr/AppleOcrEngine.cs`

**Interfaces:**
- Consumes: `IOcrEngine`, `RowCrop.ToPng(SKBitmap)`.
- Produces:
  - `sealed class AppleOcrEngine : IOcrEngine`
  - `static AppleOcrEngine AppleOcrEngine.Create(string? languageTag = null)`

- [ ] **Step 1: Write the engine**

Replace the contents of `src/Lego2STL.Core/Ocr/AppleOcrEngine.cs` with:

```csharp
using System.Linq;
using CoreGraphics;
using Foundation;
using ImageIO;
using SkiaSharp;
using Vision;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Text recognition using Apple's Vision framework.
/// </summary>
/// <remarks>
/// One class, not two: <c>VNRecognizeTextRequest</c> is the identical API on iOS and macOS,
/// so this file compiles into both the <c>net10.0-ios</c> and <c>net10.0-macos</c> targets
/// rather than being written twice.
/// </remarks>
public sealed class AppleOcrEngine : IOcrEngine
{
    private readonly string[] _languages;

    private AppleOcrEngine(string languageTag)
    {
        _languages = [languageTag];
        Name = $"Vision ({languageTag})";
    }

    public string Name { get; }

    public static AppleOcrEngine Create(string? languageTag = null)
        => new(languageTag ?? "en-US");

    public Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var png = RowCrop.ToPng(image);
        using var data = NSData.FromArray(png);
        using var source = CGImageSource.FromData(data);
        using var cgImage = source.CreateImage(0, null)
            ?? throw new InvalidOperationException("Vision could not decode the cropped row as an image.");

        var completionSource = new TaskCompletionSource<string>();

        var request = new VNRecognizeTextRequest((request, error) =>
        {
            if (error is not null)
            {
                completionSource.TrySetException(new InvalidOperationException(error.LocalizedDescription));
                return;
            }

            var observations = request.GetResults<VNRecognizedTextObservation>()
                ?? Array.Empty<VNRecognizedTextObservation>();

            // Keep the engine's own line breaks, the same choice WindowsOcrEngine and
            // AndroidOcrEngine make: the caller scans the text for the shapes it expects
            // rather than relying on any particular joining. One candidate per line - the
            // text here is short and unambiguous enough that a second guess adds nothing.
            var lines = observations
                .Select(o => o.TopCandidates(1).FirstOrDefault()?.String)
                .Where(text => !string.IsNullOrEmpty(text));

            completionSource.TrySetResult(string.Join('\n', lines));
        })
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            RecognitionLanguages = _languages,
            // Off, the same reason RowCrop crops one row under a per-row grammar rather than
            // a whole page: the text is digits and part numbers, and a language model
            // correcting toward a real word is exactly the wrong kind of help here.
            UsesLanguageCorrection = false,
        };

        using var handler = new VNImageRequestHandler(cgImage, new NSDictionary());
        handler.Perform([request], out var performError);
        if (performError is not null)
        {
            completionSource.TrySetException(new InvalidOperationException(performError.LocalizedDescription));
        }

        cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

        return completionSource.Task;
    }
}
```

**Same caveat as Task 3: written from Vision's documented API, checked against a real build.** The
ios and macos workloads are installed, so check both targets:

Run: `dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Debug -f net10.0-ios26.0`
Run: `dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Debug -f net10.0-macos26.0`

If `VNRecognizeTextRequest`'s constructor overload, or `GetResults<T>()`, or `TopCandidates(1)` do
not match what the installed binding actually exposes, fix it against the compiler's error. Run
`dotnet restore Lego2STL.slnx` afterward, per the Global Constraints note on scoped overrides.

- [ ] **Step 2: Commit**

```bash
git add src/Lego2STL.Core/Ocr/AppleOcrEngine.cs
git commit -m "feat: add the Apple text recogniser, shared between iOS and macOS"
```

---

### Task 5: Wire both engines into `OcrEngines`

**Files:**
- Modify: `src/Lego2STL.Core/Ocr/OcrEngines.cs`

**Interfaces:**
- Consumes: `AndroidOcrEngine.Create` (Task 3), `AppleOcrEngine.Create` (Task 4).
- Produces: `OcrEngines.IsAvailable` is `true` on four platforms instead of one;
  `OcrEngines.Create()` returns a real engine on four platforms instead of one; `Linux` is the only
  platform left where it throws `OcrUnavailableException`.

- [ ] **Step 1: Extend `IsAvailable`**

In `src/Lego2STL.Core/Ocr/OcrEngines.cs`, change:

```csharp
    public static bool IsAvailable =>
#if WINDOWS
        true;
#else
        false;
#endif
```

to:

```csharp
    public static bool IsAvailable =>
#if WINDOWS || ANDROID || IOS || MACOS
        true;
#else
        false;
#endif
```

- [ ] **Step 2: Extend `Create`**

Change:

```csharp
    public static IOcrEngine Create(string? languageTag = null, Strings? words = null)
    {
#if WINDOWS
        _ = words;
        return WindowsOcrEngine.Create(languageTag);
#else
        _ = languageTag;
        throw new OcrUnavailableException(DescribeUnavailable(words));
#endif
    }
```

to:

```csharp
    public static IOcrEngine Create(string? languageTag = null, Strings? words = null)
    {
#if WINDOWS
        _ = words;
        return WindowsOcrEngine.Create(languageTag);
#elif ANDROID
        _ = words;
        return AndroidOcrEngine.Create(languageTag);
#elif IOS || MACOS
        _ = words;
        return AppleOcrEngine.Create(languageTag);
#else
        _ = languageTag;
        throw new OcrUnavailableException(DescribeUnavailable(words));
#endif
    }
```

`DescribeUnavailable` is untouched — its two messages already cover "Windows, but the plain build"
and "nowhere at all," and after this task the second one is only ever true on Linux.

- [ ] **Step 3: Confirm the whole solution still builds and the suite still passes**

Run: `dotnet build Lego2STL.slnx -c Debug` (no `-f` — see Global Constraints)
Run: `dotnet test Lego2STL.slnx`
Expected: both succeed against the baseline recorded in Global Constraints. On the Windows target,
`#elif ANDROID` and `#elif IOS || MACOS` are never true, so `OcrEngines.Create()` still resolves to
exactly the branch it did before this task there.

- [ ] **Step 4: Commit**

```bash
git add src/Lego2STL.Core/Ocr/OcrEngines.cs
git commit -m "feat: OcrEngines resolves a real recogniser on android, ios and macos"
```

---

### Task 6: CI builds all five targets

**Files:**
- Modify: `.github/workflows/package.yml`

**Interfaces:**
- Consumes: nothing from earlier tasks except that `Lego2STL.Core` now has the three new targets
  to build.
- Produces: a new `mobile` job. Nothing later in this plan depends on its exact output; it is a
  leaf.

- [ ] **Step 1: Add the job**

In `.github/workflows/package.yml`, add a new job after `test` and before `windows`:

```yaml
  mobile:
    name: mobile
    needs: [version]
    # Xcode is what net10.0-ios and net10.0-macos need to build at all, and only a macOS
    # runner carries it. The android workload builds anywhere, but there is nothing to gain
    # from a second job just for it.
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install the mobile workloads
        run: dotnet workload install android ios maccatalyst

      # Build only, one target at a time, and only Core: this proves the three new
      # recognisers compile against the real bindings, which is everything a headless runner
      # can prove. Nothing here boots an emulator or a simulator, and nothing calls the real
      # recogniser - that is left to a person, with tests/Lego2STL.OcrSmokeTest.
      - name: Build Core for android
        run: dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Release -f net10.0-android36.0

      - name: Build Core for ios
        run: dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Release -f net10.0-ios26.0

      - name: Build Core for macos
        run: dotnet build src/Lego2STL.Core/Lego2STL.Core.csproj -c Release -f net10.0-macos26.0
```

`needs: [version]` only, not `needs: [test, version]`: this job says nothing about the number stamped
on a release, and does not need to wait behind the Windows-hosted `test` job to start.

- [ ] **Step 2: Confirm the workflow file is still valid YAML**

Run: `Get-Content .github/workflows/package.yml | Out-Null` — this only confirms the file reads; the
real check is `act`'s own dry parse:

```
./packaging/act/run.ps1 -n
```

Expected: act lists `mobile` among the jobs it would run, and reports (as it already does for
`windows` and `macos`) that it has no way to run it locally.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/package.yml
git commit -m "ci: build Core for android, ios and macos on every push"
```

---

### Task 7: The smoke test project and its shared fixture

**Files:**
- Create: `tests/Lego2STL.OcrSmokeTest/Lego2STL.OcrSmokeTest.csproj`
- Create: `tests/Lego2STL.OcrSmokeTest/SyntheticFixture.cs`
- Modify: `Lego2STL.slnx`

**Interfaces:**
- Consumes: `IOcrEngine` (`Lego2STL.Core`), `RowCrop` (`Lego2STL.Core`).
- Produces:
  - `sealed record SmokeResult(bool Passed, string ExpectedText, string ActualText)`
  - `static class SyntheticFixture` with
    `static SKBitmap SyntheticFixture.BuildLabelImage()` and
    `static async Task<SmokeResult> SyntheticFixture.RunAsync(IOcrEngine engine, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Create the project file**

`tests/Lego2STL.OcrSmokeTest/Lego2STL.OcrSmokeTest.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!--
      Only the three mobile targets, read from the same MobileTargetFrameworks property
      Lego2STL.Core.csproj appends in Task 1, so the three version suffixes stay pinned in
      exactly one place: this project's whole purpose is proving the real recogniser on each
      of them, so there is nothing for it to do on net10.0 or net10.0-windows, and adding
      either would need a recogniser this project does not test.
    -->
    <TargetFrameworks>$(MobileTargetFrameworks)</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <ApplicationTitle>Lego2STL OCR Smoke Test</ApplicationTitle>
    <ApplicationId>com.lego2stl.ocrsmoketest</ApplicationId>
  </PropertyGroup>

  <ItemGroup Condition="'$(IsAndroidTarget)' != 'true'">
    <Compile Remove="Platforms\Android\**\*.cs" />
  </ItemGroup>

  <ItemGroup Condition="!$(TargetFramework.Contains('-ios'))">
    <Compile Remove="Platforms\iOS\**\*.cs" />
  </ItemGroup>

  <ItemGroup Condition="!$(TargetFramework.Contains('-macos'))">
    <Compile Remove="Platforms\MacOS\**\*.cs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Lego2STL.Core\Lego2STL.Core.csproj" />
  </ItemGroup>

</Project>
```

`IsAndroidTarget` is already defined in `Directory.Build.props` from Task 1 and is true here whenever
this project builds for `net10.0-android36.0`. There is no equivalent `IsApplePlatformTarget` split
needed inside this file between iOS and macOS, because the two need different host files (a
`UIApplicationDelegate` versus a plain `Main`), so those two conditions check `TargetFramework`
directly instead.

- [ ] **Step 2: Add the shared fixture**

`tests/Lego2STL.OcrSmokeTest/SyntheticFixture.cs`:

```csharp
using Lego2STL.Core.Ocr;
using SkiaSharp;

namespace Lego2STL.OcrSmokeTest;

/// <summary>The result of running one engine against the synthetic fixture.</summary>
public sealed record SmokeResult(bool Passed, string ExpectedText, string ActualText);

/// <summary>
/// A label rendered from nothing, not cropped from a real page.
/// </summary>
/// <remarks>
/// This project's job is narrower than <c>LabelReadingAccuracyTests</c>: prove a binding is
/// wired correctly and returns real recognised text, not re-prove OCR accuracy against a
/// genuine catalogue page. A rendered fixture needs no dependency on the undistributed
/// reference PDF and carries no copyright question, which is exactly why it is the right
/// choice for a project that has to be committed.
/// </remarks>
public static class SyntheticFixture
{
    public const string ExpectedText = "5x\n32523, 11";

    /// <summary>
    /// Draws the fixture text in the same shape <see cref="RowCrop"/> produces for the real
    /// pipeline: a white margin around plain black text, at native resolution, never scaled.
    /// </summary>
    public static SKBitmap BuildLabelImage()
    {
        const int width = 160;
        const int height = 90;

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 24,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
        };

        canvas.DrawText("5x", 20, 34, paint);
        canvas.DrawText("32523, 11", 20, 66, paint);

        return bitmap;
    }

    /// <summary>Runs the given engine against the fixture and reports pass or fail.</summary>
    public static async Task<SmokeResult> RunAsync(IOcrEngine engine, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        using var image = BuildLabelImage();
        var actual = await engine.ReadAsync(image, cancellationToken).ConfigureAwait(false);

        // Compared loosely: this project checks that the binding is wired and reads real
        // text, not that punctuation and line breaks match a specific engine's habits.
        var passed = actual.Contains("32523", StringComparison.Ordinal)
            && actual.Contains("11", StringComparison.Ordinal);

        return new SmokeResult(passed, ExpectedText, actual);
    }
}
```

`BuildLabelImage` draws directly rather than calling `RowCrop.Extract` — there is no page to crop
from, only a fixture to draw — so it borrows `RowCrop`'s *shape* (a white margin around the text,
never scaled) without calling into `Lego2STL.Core.Extraction` at all.

- [ ] **Step 3: Add the project to the solution**

In `Lego2STL.slnx`, inside the existing `<Folder Name="/tests/">`, add:

```xml
    <Project Path="tests/Lego2STL.OcrSmokeTest/Lego2STL.OcrSmokeTest.csproj" />
```

- [ ] **Step 4: Confirm nothing else regressed**

Run: `dotnet build Lego2STL.slnx -c Debug` (no `-f` — see Global Constraints)
Run: `dotnet test Lego2STL.slnx`
Expected: both succeed against the baseline recorded in Global Constraints. `Lego2STL.OcrSmokeTest`
targets only the three mobile frameworks, and `dotnet test` still runs only `Lego2STL.Tests` and
`Lego2STL.UiTests` — this new project has no test framework in it and nothing
here makes `dotnet test` try to run it.

- [ ] **Step 5: Commit**

```bash
git add tests/Lego2STL.OcrSmokeTest Lego2STL.slnx
git commit -m "chore: add the OCR smoke test project and its synthetic fixture"
```

---

### Task 8: The Android host

**Files:**
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/Android/MainActivity.cs`
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/Android/AndroidManifest.xml`

**Interfaces:**
- Consumes: `SyntheticFixture.RunAsync(IOcrEngine, CancellationToken)` (Task 7),
  `OcrEngines.Create(string?, Strings?)` (`Lego2STL.Core`).
- Produces: an installable APK, once this project can actually build (Task 6's workload install is
  what makes that possible; this task cannot be built on this machine).

- [ ] **Step 1: Write the manifest**

`tests/Lego2STL.OcrSmokeTest/Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" android:versionCode="1" android:versionName="1.0">
  <uses-sdk android:minSdkVersion="24" />
  <application android:label="Lego2STL OCR Smoke Test"></application>
</manifest>
```

- [ ] **Step 2: Write the activity**

`tests/Lego2STL.OcrSmokeTest/Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.OS;
using Android.Widget;
using Lego2STL.Core.Ocr;

namespace Lego2STL.OcrSmokeTest.Platforms.Android;

/// <summary>
/// The whole of the Android smoke test: one screen, run by a person before a release,
/// exactly as far as headless CI cannot reach.
/// </summary>
[Activity(Label = "Lego2STL OCR Smoke Test", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var text = new TextView(this) { Text = "Running..." };
        SetContentView(text);

        try
        {
            var engine = OcrEngines.Create();
            var result = await SyntheticFixture.RunAsync(engine);

            text.Text = result.Passed
                ? $"PASS ({engine.Name})\n\nRead: {result.ActualText}"
                : $"FAIL ({engine.Name})\n\nExpected: {result.ExpectedText}\nRead: {result.ActualText}";
        }
        catch (Exception ex)
        {
            text.Text = $"FAIL - threw {ex.GetType().Name}: {ex.Message}";
        }
    }
}
```

- [ ] **Step 3: Record how this is actually run**

This cannot be built or run on this machine. Add the running instructions to Task 9's README rather
than duplicating them here, since that file covers all three platforms together.

- [ ] **Step 4: Commit**

```bash
git add tests/Lego2STL.OcrSmokeTest/Platforms/Android
git commit -m "feat: the OCR smoke test runs on Android"
```

---

### Task 9: The iOS and macOS hosts, and how a person runs all three

**Files:**
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/iOS/AppDelegate.cs`
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/iOS/Main.cs`
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/iOS/Info.plist`
- Create: `tests/Lego2STL.OcrSmokeTest/Platforms/MacOS/Program.cs`
- Create: `tests/Lego2STL.OcrSmokeTest/README.md`

**Interfaces:**
- Consumes: `SyntheticFixture.RunAsync(IOcrEngine, CancellationToken)` (Task 7),
  `OcrEngines.Create(string?, Strings?)` (`Lego2STL.Core`).
- Produces: nothing later in this plan depends on. This is the last task.

- [ ] **Step 1: Write the iOS entry point and view**

`tests/Lego2STL.OcrSmokeTest/Platforms/iOS/Main.cs`:

```csharp
using UIKit;

namespace Lego2STL.OcrSmokeTest.Platforms.iOS;

public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
```

`tests/Lego2STL.OcrSmokeTest/Platforms/iOS/AppDelegate.cs`:

```csharp
using Foundation;
using Lego2STL.Core.Ocr;
using UIKit;

namespace Lego2STL.OcrSmokeTest.Platforms.iOS;

/// <summary>
/// The whole of the iOS smoke test: one screen, run in the Simulator by a person before a
/// release. Vision works in the Simulator, so nothing here needs a physical device.
/// </summary>
[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        var label = new UILabel(UIScreen.MainScreen.Bounds)
        {
            Text = "Running...",
            Lines = 0,
            TextAlignment = UITextAlignment.Center,
        };

        Window.RootViewController = new UIViewController { View = label };
        Window.MakeKeyAndVisible();

        RunAsync(label);

        return true;
    }

    private static async void RunAsync(UILabel label)
    {
        try
        {
            var engine = OcrEngines.Create();
            var result = await SyntheticFixture.RunAsync(engine);

            label.Text = result.Passed
                ? $"PASS ({engine.Name})\n\nRead: {result.ActualText}"
                : $"FAIL ({engine.Name})\n\nExpected: {result.ExpectedText}\nRead: {result.ActualText}";
        }
        catch (Exception ex)
        {
            label.Text = $"FAIL - threw {ex.GetType().Name}: {ex.Message}";
        }
    }
}
```

`tests/Lego2STL.OcrSmokeTest/Platforms/iOS/Info.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key>
  <string>Lego2STL OCR Smoke Test</string>
  <key>CFBundleIdentifier</key>
  <string>com.lego2stl.ocrsmoketest</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>LSRequiresIPhoneOS</key>
  <true/>
  <key>UILaunchStoryboardName</key>
  <string></string>
</dict>
</plist>
```

- [ ] **Step 2: Write the macOS entry point**

`tests/Lego2STL.OcrSmokeTest/Platforms/MacOS/Program.cs`:

```csharp
using Lego2STL.Core.Ocr;
using Lego2STL.OcrSmokeTest;

// The whole of the macOS smoke test: no UI at all, because Vision runs headless. Run with
// `dotnet run -f net10.0-macos26.0` and read the result off stdout.
try
{
    var engine = OcrEngines.Create();
    var result = await SyntheticFixture.RunAsync(engine);

    Console.WriteLine(result.Passed
        ? $"PASS ({engine.Name})"
        : $"FAIL ({engine.Name}) - expected \"{result.ExpectedText}\", read \"{result.ActualText}\"");

    Environment.Exit(result.Passed ? 0 : 1);
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL - threw {ex.GetType().Name}: {ex.Message}");
    Environment.Exit(1);
}
```

- [ ] **Step 3: Write the README that ties all three together**

`tests/Lego2STL.OcrSmokeTest/README.md`:

```markdown
# OCR smoke test

Proves each mobile recogniser is wired correctly and reads real text. It does not prove OCR
accuracy against a genuine catalogue page — that stays `LabelReadingAccuracyTests`, gated on
Windows and the reference document.

Nothing here runs in CI. CI (the `mobile` job in `.github/workflows/package.yml`) only proves
these three targets compile; a person runs this project by hand, on a device, emulator or
simulator, before a release that claims mobile OCR works.

| Platform | Run | Read the result |
|---|---|---|
| Android | Deploy `Lego2STL.OcrSmokeTest` to an emulator or device from Visual Studio / `dotnet build -t:Run -f net10.0-android36.0` | On screen |
| iOS | Run in the Simulator from Visual Studio for Mac / Xcode, or `dotnet build -t:Run -f net10.0-ios26.0` | On screen |
| macOS | `dotnet run --project tests/Lego2STL.OcrSmokeTest -f net10.0-macos26.0` | On stdout |

A PASS reads the fixture's part number and colour code back; a FAIL names what went wrong,
including an exception, since the smoke test's own failure has to be as legible as a real
one.
```

- [ ] **Step 4: Update the project file's exclusions to cover the two new Platform folders**

Task 7 already added `Compile Remove` groups for `Platforms\Android`, `Platforms\iOS` and
`Platforms\MacOS`. No further csproj change is needed here — this step exists only to confirm it:
open `tests/Lego2STL.OcrSmokeTest/Lego2STL.OcrSmokeTest.csproj` and check all three groups are
present. If one was missed in Task 7, add it now, following the same pattern.

- [ ] **Step 5: Record the phase complete**

Append to `PROGRESS.md`:

```
PHASE:D WAVE:9 STATUS:complete TS:<ISO-8601-UTC>
PHASE:D WAVE:0 STATUS:complete TS:<ISO-8601-UTC>
```

- [ ] **Step 6: Commit**

```bash
git add tests/Lego2STL.OcrSmokeTest
git commit -m "feat: the OCR smoke test runs on iOS and macOS, with a README for all three"
```

---

## Notes for whoever executes this

- **Tasks 3, 4, 7, 8 and 9 can now be compiled on this machine.** The android, ios, maccatalyst and
  macos workloads were installed during Task 1 (see Global Constraints) specifically because their
  absence broke restore for the entire solution, not just the three new targets. With them present,
  `dotnet build Lego2STL.slnx -c Debug` is a real signal for every task in this plan — treat it as
  a gate, not an aspiration. What still cannot happen here is running the smoke test project for
  real (Task 7 onward): that needs a booted emulator, simulator or a Mac, and Task 6's CI job
  remains the only place proving the exact workload versions CI's own runners resolve to.
- **If a binding namespace or method name doesn't match what compiles**, fix it against the real
  compiler error now that it's checkable locally — the caveat in Tasks 3 and 4 that this is expected
  engineering work against a binding never used in this repository still applies; only the "wait for
  CI to find out" part of it is now optional.
- **`Directory.Build.props`'s shared `TargetFrameworks` is deliberately untouched.** The three new
  targets live only in `Lego2STL.Core.csproj`, through `MobileTargetFrameworks`. If a later task
  seems to need `Lego2STL.Cli` or `Lego2STL.Gui` to build for a mobile target, stop — that is Phase
  E, and pulling it into Phase D here would build heads for platforms this phase never designed a UI
  for.
- **The smoke test's fixture is synthetic on purpose.** Do not swap it for a crop of the reference
  document — that file is not committed, and this project has to build (even if it cannot run) in
  environments that do not have it.
- Record `PHASE:D WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` after each task, and
  `PHASE:D WAVE:0 STATUS:complete` when all nine are done.
