# Android And iOS — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Avalonia window as an Android and an iOS application over the same view models, with a run folder that lives in application storage, results that leave by a share sheet, and a geometry source that never downloads the 80 MB library on a phone.

**Architecture:** `Lego2STL.Gui` becomes a four-target project rather than a desktop executable: the window's content moves into a `MainView` `UserControl` so `App` can serve `ISingleViewApplicationLifetime` as well as the classic desktop one, and per-platform entry points sit under `Platforms/`. Core gains one seam, `IRunHome`, which decides where a run's folder goes; the Gui keeps the share-sheet seam, because sharing is a UI act. CI grows an APK, an iOS simulator build, and a self-driving smoke head launched on a booted emulator and simulator.

**Tech Stack:** .NET 10, Avalonia 12.1.1 (`Avalonia.Android`, `Avalonia.iOS`), CommunityToolkit.Mvvm 8.4.2, xUnit with `Avalonia.Headless.XUnit`, FluentAssertions, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-02-android-and-ios-design.md`

## Global Constraints

- **Target frameworks are pinned in one place.** `Directory.Build.props` owns every version suffix. The Gui's mobile pair is `net10.0-android36.0;net10.0-ios26.0` — Android and iOS only. **macOS is not a Gui target**: Avalonia's desktop target already covers it through `net10.0`.
- **Avalonia stays at 12.1.1** across all six packages. No version split between desktop and mobile.
- **`minSdkVersion` in `AndroidManifest.xml` and `SupportedOSPlatformVersion` in the csproj must be the same number**, or the SDK refuses with `XA1036`. Both are **24** (a transitive ML Kit dependency, `androidx.lifecycle.runtime`, needs 23 or higher).
- **An `Info.plist` outside the project root is silently ignored** unless `<InfoPlist>` names it. The failure has no error: the app just has no recognisable name or bundle id.
- **A missing workload fails restore for every project that references the changed one**, not only the new target. Since this plan changes `Lego2STL.Gui`, that now reaches `Lego2STL.UiTests` too. Every packaging call needs `-p:TargetFrameworks=<one value>`, and `dotnet publish` needs `-f <same value>` alongside it or it refuses with `NETSDK1129`.
- **Never pass a solution-wide `-f`.** `Lego2STL.Tests` and `Lego2STL.UiTests` only target `net10.0-windows10.0.19041.0`. A project-scoped override also leaves a stale `project.assets.json` that the next unscoped build reuses — run `dotnet restore Lego2STL.slnx` afterwards.
- **The test command is** `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`.
- **The green baseline is not "all green".** `Lego2STL.Tests` 653/653 always; `Lego2STL.UiTests` green except `CalibrationPlateTests.With_nothing_to_build_from_it_says_so_and_writes_no_shapes`, which fails on unmodified `main`, plus a varying set of tests that fail only under full-suite contention and pass in isolation (`CalibrationManagementTests`, `TolerancesReachABuildTests`, `OptionRoundTripTests`). Judge regressions against that, and do not "fix" those in this plan.
- **The local build gate is** `dotnet build Lego2STL.slnx -c Debug`. The android/ios/maccatalyst/macos workloads are installed on this machine, so it is a real signal.
- **A new `TextKey` must be added to English and Italian both.** Adding one during A+B exposed three window tests that switched language and never switched it back; if that happens again, restore the language in the test rather than removing the key.
- **PROGRESS protocol.** Read `PROGRESS.md` before each task; append `PHASE:E WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>` immediately after each, and `PHASE:E WAVE:0` when all eleven are done.
- **Comments and changelog stay to one sentence.** Test comments are exempt.

---

### Task 1: The window's content becomes a view

**Files:**
- Create: `src/Lego2STL.Gui/Views/MainView.axaml`
- Create: `src/Lego2STL.Gui/Views/MainView.axaml.cs`
- Modify: `src/Lego2STL.Gui/Views/MainWindow.axaml` (167 lines — everything between `<DockPanel>` and `</DockPanel>`, plus `<Window.Styles>`, moves out)
- Modify: `src/Lego2STL.Gui/App.axaml.cs:15-22`
- Test: `tests/Lego2STL.UiTests/MainViewTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Lego2STL.Gui.Views.MainView : UserControl`, default-constructible, expecting a `MainViewModel` as `DataContext`. `MainWindow` keeps its parameterless constructor and now sets `Content = new MainView()`.

- [ ] **Step 1: Write the failing test**

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The window's content, drawn on its own rather than inside a window.
/// </summary>
/// <remarks>
/// A phone has no window to host, so everything the window shows has to be a control that
/// stands by itself. Drawing it outside a Window is exactly what the single-view lifetime
/// will do on Android and iOS, so this test is that lifetime rehearsed on the desktop.
/// </remarks>
public sealed class MainViewTests
{
    [AvaloniaFact]
    public void The_view_draws_without_a_window_of_its_own()
    {
        using var model = new MainViewModel();

        var window = new Window { Width = 1040, Height = 720, Content = new MainView { DataContext = model } };

        window.Show();
        window.CaptureRenderedFrame().Should().NotBeNull();
    }

    [AvaloniaFact]
    public void The_window_hosts_the_same_view()
    {
        using var model = new MainViewModel();

        var window = new MainWindow { DataContext = model };

        window.Show();
        window.CaptureRenderedFrame();

        window.Content.Should().BeOfType<MainView>();
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.UiTests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~MainViewTests`
Expected: FAIL — `MainView` does not exist (`CS0246`).

- [ ] **Step 3: Move the content into `MainView.axaml`**

Create `src/Lego2STL.Gui/Views/MainView.axaml` as a `UserControl` whose root is the `<DockPanel>` currently inside `MainWindow.axaml`, carrying the whole `<Window.Styles>` block across as `<UserControl.Styles>`. Header:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Lego2STL.Gui.ViewModels"
             xmlns:loc="using:Lego2STL.Gui.Localization"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d" d:DesignWidth="1040" d:DesignHeight="720"
             x:Class="Lego2STL.Gui.Views.MainView"
             x:DataType="vm:MainViewModel"
             Background="{DynamicResource AppWindowBackground}">
```

Do not retype the body: move the existing markup verbatim, so nothing about the four screens changes.

Code-behind, `src/Lego2STL.Gui/Views/MainView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Lego2STL.Gui.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Reduce `MainWindow.axaml` to a shell**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Lego2STL.Gui.ViewModels"
        xmlns:views="using:Lego2STL.Gui.Views"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="1040" d:DesignHeight="720"
        x:Class="Lego2STL.Gui.Views.MainWindow"
        x:DataType="vm:MainViewModel"
        Width="1040" Height="720"
        MinWidth="820" MinHeight="560"
        Background="{DynamicResource AppWindowBackground}"
        Title="Lego2STL"
        Icon="/Assets/icon.ico">

  <views:MainView />

</Window>
```

`MainWindow.axaml.cs` is unchanged.

- [ ] **Step 5: Serve both lifetimes from `App`**

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var model = new MainViewModel();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow { DataContext = model };
        desktop.ShutdownRequested += (_, _) => model.Dispose();
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
    {
        // A phone has no shutdown to hook: the view model lives as long as the process.
        single.MainView = new MainView { DataContext = model };
    }

    base.OnFrameworkInitializationCompleted();
}
```

- [ ] **Step 6: Run the new test and then the whole suite**

Run: `dotnet test tests/Lego2STL.UiTests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~MainViewTests`
Expected: PASS, both.

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline in Global Constraints. All 26 existing window tests must still pass — they draw `MainWindow`, which now draws `MainView`.

- [ ] **Step 7: Commit and record**

```bash
git add src/Lego2STL.Gui/Views/MainView.axaml src/Lego2STL.Gui/Views/MainView.axaml.cs src/Lego2STL.Gui/Views/MainWindow.axaml src/Lego2STL.Gui/App.axaml.cs tests/Lego2STL.UiTests/MainViewTests.cs PROGRESS.md
git commit -m "refactor: the window's content becomes a view that can stand alone"
```

---

### Task 2: The Gui gains an Android and an iOS target

**Files:**
- Modify: `Directory.Build.props:41-45`
- Modify: `src/Lego2STL.Gui/Lego2STL.Gui.csproj`
- Create: `src/Lego2STL.Gui/Platforms/Android/MainActivity.cs`
- Create: `src/Lego2STL.Gui/Platforms/Android/AndroidManifest.xml`
- Create: `src/Lego2STL.Gui/Platforms/iOS/AppDelegate.cs`
- Create: `src/Lego2STL.Gui/Platforms/iOS/Main.cs`
- Create: `src/Lego2STL.Gui/Platforms/iOS/Info.plist`
- Modify: `src/Lego2STL.Gui/Program.cs` (unchanged content; excluded from mobile targets by the csproj)

**Interfaces:**
- Consumes: `MainView` and the two-lifetime `App` from Task 1.
- Produces: `$(GuiMobileTargetFrameworks)` in `Directory.Build.props`; an Android application id `com.lego2stl.app`; the Gui building for four targets.

- [ ] **Step 1: Pin the suffixes once, and add the Gui's pair**

In `Directory.Build.props`, replace the single `MobileTargetFrameworks` line with:

```xml
<AndroidTargetFramework>net10.0-android36.0</AndroidTargetFramework>
<IosTargetFramework>net10.0-ios26.0</IosTargetFramework>
<MacOsTargetFramework>net10.0-macos26.0</MacOsTargetFramework>
<!-- Core's three: the recogniser needs macOS, the window does not. -->
<MobileTargetFrameworks>$(AndroidTargetFramework);$(IosTargetFramework);$(MacOsTargetFramework)</MobileTargetFrameworks>
<!-- The window's two: Avalonia's desktop target already covers macOS through net10.0. -->
<GuiMobileTargetFrameworks>$(AndroidTargetFramework);$(IosTargetFramework)</GuiMobileTargetFrameworks>
```

- [ ] **Step 2: Give the Gui its targets, packages and exclusions**

In `src/Lego2STL.Gui/Lego2STL.Gui.csproj`, add to the first `PropertyGroup`:

```xml
<TargetFrameworks>$(TargetFrameworks);$(GuiMobileTargetFrameworks)</TargetFrameworks>
<OutputType Condition="'$(IsAndroidTarget)' == 'true' OR $(TargetFramework.Contains('-ios'))">Exe</OutputType>
<ApplicationId>com.lego2stl.app</ApplicationId>
```

Condition the desktop-only package and add the two mobile ones:

```xml
<ItemGroup Condition="'$(IsAndroidTarget)' != 'true' AND !$(TargetFramework.Contains('-ios'))">
  <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
</ItemGroup>

<ItemGroup Condition="'$(IsAndroidTarget)' == 'true'">
  <PackageReference Include="Avalonia.Android" Version="12.1.1" />
</ItemGroup>

<ItemGroup Condition="$(TargetFramework.Contains('-ios'))">
  <PackageReference Include="Avalonia.iOS" Version="12.1.1" />
</ItemGroup>
```

Exclude each platform's files from the others, and the desktop entry point from both mobile targets:

```xml
<ItemGroup Condition="'$(IsAndroidTarget)' != 'true'">
  <Compile Remove="Platforms\Android\**\*.cs" />
</ItemGroup>

<ItemGroup Condition="!$(TargetFramework.Contains('-ios'))">
  <Compile Remove="Platforms\iOS\**\*.cs" />
</ItemGroup>

<ItemGroup Condition="'$(IsAndroidTarget)' == 'true' OR $(TargetFramework.Contains('-ios'))">
  <Compile Remove="Program.cs" />
</ItemGroup>

<PropertyGroup Condition="'$(IsAndroidTarget)' == 'true'">
  <AndroidManifest>Platforms\Android\AndroidManifest.xml</AndroidManifest>
  <!-- Must equal the manifest's own minSdkVersion exactly, or the SDK refuses with XA1036. -->
  <SupportedOSPlatformVersion>24.0</SupportedOSPlatformVersion>
</PropertyGroup>

<PropertyGroup Condition="$(TargetFramework.Contains('-ios'))">
  <!-- Not the default root location, and an unnamed plist is ignored in silence. -->
  <InfoPlist>Platforms\iOS\Info.plist</InfoPlist>
</PropertyGroup>
```

The existing `ApplicationManifest` and `ApplicationIcon` lines stay: the first is already conditioned on `IsWindowsTarget`, and the second is ignored off Windows.

- [ ] **Step 3: Write the Android entry point and manifest**

`src/Lego2STL.Gui/Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Lego2STL.Gui.Platforms.Android;

[Activity(
    Label = "Lego2STL",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
```

`src/Lego2STL.Gui/Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.lego2stl.app">
  <!-- 24, not the SDK's default of 21: a transitive ML Kit dependency needs 23 or higher. -->
  <uses-sdk android:minSdkVersion="24" android:targetSdkVersion="36" />
  <uses-permission android:name="android.permission.INTERNET" />
  <application android:label="Lego2STL" android:allowBackup="true" />
</manifest>
```

- [ ] **Step 4: Write the iOS entry point and plist**

`src/Lego2STL.Gui/Platforms/iOS/Main.cs`:

```csharp
using UIKit;

namespace Lego2STL.Gui.Platforms.iOS;

public static class Application
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
```

`src/Lego2STL.Gui/Platforms/iOS/AppDelegate.cs`:

```csharp
using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace Lego2STL.Gui.Platforms.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
```

`src/Lego2STL.Gui/Platforms/iOS/Info.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key><string>Lego2STL</string>
  <key>CFBundleIdentifier</key><string>com.lego2stl.app</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>MinimumOSVersion</key><string>15.0</string>
  <key>UIDeviceFamily</key><array><integer>1</integer><integer>2</integer></array>
  <key>UILaunchStoryboardName</key><string></string>
  <key>UIRequiredDeviceCapabilities</key><array><string>armv7</string></array>
  <key>UISupportedInterfaceOrientations</key>
  <array>
    <string>UIInterfaceOrientationPortrait</string>
    <string>UIInterfaceOrientationLandscapeLeft</string>
    <string>UIInterfaceOrientationLandscapeRight</string>
  </array>
</dict>
</plist>
```

- [ ] **Step 5: Build each target and watch what breaks**

Run: `dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Debug -f net10.0-android36.0`
Expected: PASS. If it fails with `XAAMM0000: Namespace ... is used in multiple modules`, that is the `minSdkVersion` disagreement of Step 3, not a duplicate dependency — check the two numbers agree.

Run: `dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Debug -f net10.0-ios26.0`
Expected: PASS. `CS5001: Program does not contain a static 'Main'` means `Platforms/iOS/Main.cs` is being excluded — check the `Compile Remove` conditions.

Run: `dotnet restore Lego2STL.slnx && dotnet build Lego2STL.slnx -c Debug`
Expected: PASS for all projects — the restore is not optional, because the two project-scoped builds above leave a stale `project.assets.json`.

- [ ] **Step 6: Prove the desktop application is untouched**

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline.

Run: `pwsh packaging/build-windows.ps1` (or the shortest publish it wraps)
Expected: PASS. If it dies with `NU1102: Unable to find package Microsoft.NETCore.App.Runtime.Mono.win-x64`, the publish call needs `-p:TargetFrameworks=net10.0-windows10.0.19041.0 -f net10.0-windows10.0.19041.0`; add it there.

- [ ] **Step 7: Commit and record**

```bash
git add Directory.Build.props src/Lego2STL.Gui/Lego2STL.Gui.csproj src/Lego2STL.Gui/Platforms packaging PROGRESS.md
git commit -m "feat: the window builds for Android and iOS"
```

> **What the real build corrected (Task 2).** Four things Step 3-5 as written did not survive contact
> with Avalonia 12.1.1 and the installed Android workload:
>
> 1. **`AvaloniaMainActivity<App>` does not exist in 12.1.1.** `AvaloniaMainActivity` is non-generic
>    now; the `AppBuilder` is built by an `Android.App.Application` subclass instead —
>    `AvaloniaAndroidApplication<TApp>`, marked `[Application]`. `CustomizeAppBuilder` moved there.
>    Added `Platforms/Android/MainApplication.cs` alongside `MainActivity.cs`; `MainActivity` is now
>    just `public sealed class MainActivity : AvaloniaMainActivity;` with the `[Activity]` attribute.
> 2. **The Android target's implicit global usings collide with Avalonia's own types.** The .NET
>    Android SDK adds `global using Android.App;` and `global using Android.Widget;` whenever
>    `ImplicitUsings` is enabled, which makes `Application` (in `App.axaml.cs`) and `Button` (in
>    `Services/Clip.cs`, `Views/OptionListView.axaml.cs`) ambiguous with Avalonia's own types of the
>    same name. Fixed with `<Using Remove="Android.App" />` and `<Using Remove="Android.Widget" />`
>    in an Android-only `ItemGroup`, plus spelling out `Avalonia.Application` as `App`'s base type.
> 3. **`Icon = "@drawable/icon"` and `Theme = "@style/MyTheme.NoActionBar"` on `[Activity]` name
>    resources that do not exist anywhere in this repository** — there is no Android resource
>    folder at all yet. Added `Platforms/Android/Resources/values/styles.xml` (one theme, extending
>    `android:Theme.Material.NoActionBar`) and `Platforms/Android/Resources/drawable/icon.xml` (a
>    flat-colour placeholder vector — a real launcher icon is design work, out of scope here).
>    Because these resources sit beside the manifest rather than at the project root, they also
>    needed `<MonoAndroidResourcePrefix>Platforms\Android\Resources</MonoAndroidResourcePrefix>` — the
>    default resource glob only looks under a root-level `Resources\` folder.
>
> None of this changes the plan's stated interfaces or file list beyond one added file
> (`MainApplication.cs`) and two added resource files; it is exactly the shape Avalonia's Android
> template itself uses, just not the shape this plan's Step 3 assumed.

---

### Task 3: The sidebar collapses on a narrow screen

**Files:**
- Create: `src/Lego2STL.Gui/Views/NavigationRailView.axaml`
- Create: `src/Lego2STL.Gui/Views/NavigationRailView.axaml.cs`
- Modify: `src/Lego2STL.Gui/Views/MainView.axaml` (the `Border DockPanel.Dock="Left" Width="200"` block, and the top bar)
- Modify: `src/Lego2STL.Gui/Views/MainView.axaml.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/MainViewModel.cs`
- Test: `tests/Lego2STL.UiTests/CompactLayoutTests.cs`

**Interfaces:**
- Consumes: `MainView` from Task 1.
- Produces: `MainViewModel.IsCompact` (`bool`, observable, settable); `MainView.CompactWidth` (`public const double`, value `700`); `NavigationRailView : UserControl` holding the rail markup, used both docked and inside the flyout.

- [ ] **Step 1: Write the failing test**

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// What the window does when it is only as wide as a phone.
/// </summary>
/// <remarks>
/// The threshold is a property of the view and not of the platform, so a narrow desktop
/// window behaves exactly as a phone does - which is what makes this testable at all,
/// since no test here runs on a phone. 360 x 780 is a common small Android screen in
/// device-independent pixels; 1040 x 720 is the desktop window's own size.
/// </remarks>
public sealed class CompactLayoutTests
{
    private static Window Showing(MainViewModel model, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = new MainView { DataContext = model } };
        window.Show();
        window.CaptureRenderedFrame();
        return window;
    }

    [AvaloniaFact]
    public void A_desktop_width_keeps_the_rail_docked()
    {
        using var model = new MainViewModel();

        using var window = Showing(model, 1040, 720);

        model.IsCompact.Should().BeFalse();
    }

    [AvaloniaFact]
    public void A_phone_width_collapses_the_rail()
    {
        using var model = new MainViewModel();

        using var window = Showing(model, 360, 780);

        model.IsCompact.Should().BeTrue();
    }

    [AvaloniaFact]
    public void The_view_still_draws_at_a_phone_size()
    {
        using var model = new MainViewModel();

        using var window = Showing(model, 360, 780);

        window.CaptureRenderedFrame().Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.UiTests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~CompactLayoutTests`
Expected: FAIL — `MainViewModel` has no `IsCompact` (`CS1061`).

- [ ] **Step 3: Add the property**

In `MainViewModel`, beside the other observable properties:

```csharp
/// <summary>True when the window is too narrow to keep the rail beside the page.</summary>
[ObservableProperty]
private bool _isCompact;
```

- [ ] **Step 4: Move the rail into its own view**

Create `NavigationRailView.axaml` as a `UserControl` with `x:DataType="vm:MainViewModel"` whose root is the `<DockPanel>` currently inside the left-hand `Border` of `MainView.axaml` — the new-run button and the page list, moved verbatim. Code-behind is the three-line `InitializeComponent` partial, exactly like `MainView.axaml.cs`.

- [ ] **Step 5: Use it in both places**

In `MainView.axaml`, the docked border becomes:

```xml
<Border DockPanel.Dock="Left" Width="200" Padding="8,6,8,10"
        BorderThickness="0,0,1,0"
        BorderBrush="{DynamicResource AppCardBorder}"
        IsVisible="{Binding !IsCompact}">
  <views:NavigationRailView />
</Border>
```

and the top bar gains a button that carries the same rail in a flyout, before the language block:

```xml
<Button Padding="10,6" IsVisible="{Binding IsCompact}"
        Content="{Binding [UiPages], Source={x:Static loc:Loc.Current}}">
  <Button.Flyout>
    <Flyout Placement="BottomEdgeAlignedLeft">
      <Border Width="240" Padding="8">
        <views:NavigationRailView />
      </Border>
    </Flyout>
  </Button.Flyout>
</Button>
```

Add `xmlns:views="using:Lego2STL.Gui.Views"` to the `UserControl` header, and make the top bar's `StackPanel` `HorizontalAlignment="Stretch"` with the menu button at the left and the language block at the right.

`UiPages` is a new `TextKey` — add it to `TextKey.cs`, `Strings.English.cs` (`"Pages"`) and `Strings.Italian.cs` (`"Pagine"`), and to whatever the Gui's `Loc` indexer reads. If a window test then fails because it switched language and never switched back, restore the language in that test.

- [ ] **Step 6: Drive the property from the view's own width**

`MainView.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class MainView : UserControl
{
    /// <summary>Below this width the rail cannot sit beside the page, so it folds into a flyout.</summary>
    public const double CompactWidth = 700;

    public MainView()
    {
        InitializeComponent();

        // The view's own width, not the platform: a narrow desktop window folds the same way,
        // which is what lets the behaviour be tested without a device.
        this.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is MainViewModel model)
            {
                model.IsCompact = bounds.Width > 0 && bounds.Width < CompactWidth;
            }
        });
    }
}
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/Lego2STL.UiTests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~CompactLayoutTests`
Expected: PASS, all three.

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline.

- [ ] **Step 8: Commit and record**

```bash
git add src/Lego2STL.Gui/Views src/Lego2STL.Gui/ViewModels/MainViewModel.cs src/Lego2STL.Core/Text tests/Lego2STL.UiTests/CompactLayoutTests.cs PROGRESS.md
git commit -m "feat: the rail folds away when the window is as narrow as a phone"
```

> **What the real build corrected (Task 3).** Three small things:
>
> 1. **`Window` is not `IDisposable`** in this Avalonia version, unlike the plan's `using var window`
>    in the test snippet. Dropped the `using`, matching Task 1's `MainViewTests`, which never used one.
> 2. **`MainViewModel` in this codebase already uses CommunityToolkit.Mvvm's partial-property
>    `[ObservableProperty]` style** (`public partial bool IsCompact { get; set; }`), not the
>    plan's `[ObservableProperty] private bool _isCompact;` field style. Followed the file's own
>    established convention instead of the plan's literal snippet; the produced member is identical.
> 3. **`this.GetObservable(BoundsProperty).Subscribe(lambda)` does not compile.** `IObservable<T>`'s
>    own instance `Subscribe(IObserver<T>)` wins overload resolution ahead of any `Subscribe(Action<T>)`
>    extension, because member lookup stops at the first name match regardless of applicability.
>    Avalonia's own `Observable.Subscribe(Action<T>)` extension exists but its containing class is
>    `internal`, so it cannot be named from application code either. Fixed by wrapping the lambda in
>    Avalonia's public `Avalonia.Reactive.AnonymousObserver<T>`.

---

### Task 4: A run's folder becomes a decision, not a calculation

**Files:**
- Create: `src/Lego2STL.Core/Run/IRunHome.cs`
- Create: `src/Lego2STL.Core/Run/BesideTheInputRunHome.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs:31-45` (constructor and field), and its four `RunLayout.Plan(settings)` call sites at lines 62, 253, 509 and 526
- Test: `tests/Lego2STL.Tests/Run/RunHomeTests.cs`

**Interfaces:**
- Consumes: `RunLayout.Plan(RunSettings)` and `RunLayout.For(string, string?)`, both existing.
- Produces:

```csharp
public interface IRunHome
{
    RunLayout? Plan(RunSettings settings);
}

public sealed class BesideTheInputRunHome : IRunHome { }
```

and `PipelineRunner(Action<string>? log = null, IProgress<RunProgress>? progress = null, IRunHome? home = null)`. Every existing call site keeps working, because `home` defaults to `BesideTheInputRunHome`.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Xunit;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Where a run decides to put its folder.
/// </summary>
/// <remarks>
/// "Beside the input" is a decision and not a law of nature: a sandboxed application has no
/// such place, because the document arrives from a picker. These check that the decision is
/// now something a caller can supply, and that supplying nothing keeps the old behaviour
/// exactly - which is what every desktop and command-line run depends on.
/// </remarks>
public sealed class RunHomeTests
{
    [Fact]
    public void Beside_the_input_is_what_a_run_does_when_nobody_says_otherwise()
    {
        var input = Path.Combine(Path.GetTempPath(), "lego2stl-home", "6324712.csv");
        var settings = new RunSettings { InputPath = input };

        var layout = new BesideTheInputRunHome().Plan(settings);

        layout.Should().NotBeNull();
        layout!.Root.Should().Be(RunLayout.Plan(settings)!.Root);
        layout.Name.Should().Be("6324712");
    }

    [Fact]
    public void An_input_too_incomplete_to_name_a_folder_plans_nothing()
    {
        new BesideTheInputRunHome().Plan(new RunSettings()).Should().BeNull();
    }

    [Fact]
    public void A_home_of_its_own_puts_the_run_where_it_says()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-elsewhere");
        var settings = new RunSettings { InputPath = Path.Combine(Path.GetTempPath(), "in", "6324712.csv") };

        var layout = new StubHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "6324712"));
    }

    private sealed class StubHome(string root) : IRunHome
    {
        public RunLayout? Plan(RunSettings settings) =>
            settings.InputPath is null ? null : RunLayout.For(settings.InputPath, root);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~RunHomeTests`
Expected: FAIL — `IRunHome` does not exist.

- [ ] **Step 3: Write the seam**

`src/Lego2STL.Core/Run/IRunHome.cs`:

```csharp
using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>
/// Decides where a run's folder goes.
/// </summary>
/// <remarks>
/// A desktop run puts it beside the input, which is what every command line has always done.
/// A sandboxed application cannot: the document arrives from a picker and there is nowhere
/// beside it to write. The decision therefore belongs to whoever is running the pipeline,
/// the same way the recogniser does.
/// </remarks>
public interface IRunHome
{
    /// <summary>The folder this run will use, or null when the settings do not yet name one.</summary>
    RunLayout? Plan(RunSettings settings);
}
```

`src/Lego2STL.Core/Run/BesideTheInputRunHome.cs`:

```csharp
using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>The original behaviour: one folder beside the input file.</summary>
public sealed class BesideTheInputRunHome : IRunHome
{
    public RunLayout? Plan(RunSettings settings) => RunLayout.Plan(settings);
}
```

- [ ] **Step 4: Let the pipeline ask instead of calculate**

In `PipelineRunner`, add the field and constructor parameter:

```csharp
private readonly IRunHome _home;

public PipelineRunner(
    Action<string>? log = null,
    IProgress<RunProgress>? progress = null,
    IRunHome? home = null)
{
    ...
    _home = home ?? new BesideTheInputRunHome();
}
```

Then replace each of the four `RunLayout.Plan(settings)` calls (lines 62, 253, 509, 526) with `_home.Plan(settings)`, keeping the existing `!` where it is already there. Change nothing else: `RunLayout` itself is untouched.

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~RunHomeTests`
Expected: PASS, all three.

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline. The `PipelineManifestTests` all construct `PipelineRunner` without a home and must be unaffected.

- [ ] **Step 6: Commit and record**

```bash
git add src/Lego2STL.Core/Run/IRunHome.cs src/Lego2STL.Core/Run/BesideTheInputRunHome.cs src/Lego2STL.Core/Pipeline/PipelineRunner.cs tests/Lego2STL.Tests/Run/RunHomeTests.cs PROGRESS.md
git commit -m "refactor: where a run writes becomes something the caller decides"
```

---

### Task 5: A home in application storage, and the document that gets copied there

**Files:**
- Create: `src/Lego2STL.Core/Run/ApplicationStorageRunHome.cs`
- Create: `src/Lego2STL.Gui/Services/DocumentImport.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs:524` (pass the home in)
- Modify: `src/Lego2STL.Gui/Services/UserSettings.cs` (expose the storage root)
- Test: `tests/Lego2STL.Tests/Run/ApplicationStorageRunHomeTests.cs`
- Test: `tests/Lego2STL.UiTests/DocumentImportTests.cs`

**Interfaces:**
- Consumes: `IRunHome` from Task 4.
- Produces:

```csharp
public sealed class ApplicationStorageRunHome(string root) : IRunHome
{
    public string Root { get; }
    public RunLayout? Plan(RunSettings settings);
}

public static class DocumentImport
{
    public static async Task<string> CopyInAsync(Stream source, string fileName, string root, CancellationToken cancellationToken = default);
}
```

`CopyInAsync` returns the full path of the copy. `ApplicationStorageRunHome.Plan` puts the run folder under `root`, named after the input file or, for a set, after `RunLayout.SetFolderName`.

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Xunit;

namespace Lego2STL.Tests.Run;

/// <summary>
/// A run's folder on a machine where "beside the input" does not exist.
/// </summary>
/// <remarks>
/// The point of the copy-in step is that a run folder on a phone ends up the same shape as a
/// run folder on a desktop, so the runs list - which reads folders and knows nothing about
/// platforms - keeps working untouched.
/// </remarks>
public sealed class ApplicationStorageRunHomeTests
{
    [Fact]
    public void A_document_run_lands_under_the_storage_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var settings = new RunSettings { InputPath = Path.Combine(root, "imports", "6324712.pdf") };

        var layout = new ApplicationStorageRunHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "6324712"));
        layout.Name.Should().Be("6324712");
    }

    [Fact]
    public void A_set_run_lands_under_the_storage_root_too()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var settings = new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        var layout = new ApplicationStorageRunHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "set-42100-1"));
    }

    [Fact]
    public void Nothing_to_name_a_folder_from_plans_nothing()
    {
        new ApplicationStorageRunHome(Path.GetTempPath()).Plan(new RunSettings()).Should().BeNull();
    }

    [Fact]
    public void An_explicit_output_directory_still_wins()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var chosen = Path.Combine(Path.GetTempPath(), "lego2stl-chosen");
        var settings = new RunSettings
        {
            InputPath = Path.Combine(root, "imports", "6324712.pdf"),
            OutputDirectory = chosen,
        };

        new ApplicationStorageRunHome(root).Plan(settings)!.Root
            .Should().Be(Path.Combine(chosen, "6324712"));
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~ApplicationStorageRunHomeTests`
Expected: FAIL — `ApplicationStorageRunHome` does not exist.

- [ ] **Step 3: Write the home**

```csharp
using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>
/// One folder under application storage, for a platform that has nowhere else to write.
/// </summary>
/// <remarks>
/// The picked document is copied into the same root before the run starts, so "under
/// application storage" and "beside the input" name the same folder and a run's contents
/// look identical to a desktop run's.
/// </remarks>
public sealed class ApplicationStorageRunHome : IRunHome
{
    public ApplicationStorageRunHome(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>Where every run this application makes is written.</summary>
    public string Root { get; }

    public RunLayout? Plan(RunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // An explicit output directory is a person's instruction and outranks the default.
        var root = settings.OutputDirectory ?? Root;

        if (settings.Kind == InputKind.SetNumber)
        {
            return string.IsNullOrWhiteSpace(settings.SetNumber)
                ? null
                : RunLayout.At(Path.Combine(root, RunLayout.SetFolderName(settings.SetNumber)));
        }

        return string.IsNullOrWhiteSpace(settings.InputPath)
            ? null
            : RunLayout.For(settings.InputPath, root);
    }
}
```

- [ ] **Step 4: Write the copy-in, and its test**

`src/Lego2STL.Gui/Services/DocumentImport.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Brings a picked document into application storage, where a run can be written beside it.
/// </summary>
/// <remarks>
/// A document picker hands over a stream, not a path, and on Android and iOS the place it
/// came from is not ours to write to. Copying costs a second copy of the input on the device,
/// which is the honest price of a sandbox and is the user's to delete.
/// </remarks>
public static class DocumentImport
{
    public static async Task<string> CopyInAsync(
        Stream source,
        string fileName,
        string root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var imports = Path.Combine(Path.GetFullPath(root), "imports");
        Directory.CreateDirectory(imports);

        var destination = Path.Combine(imports, Path.GetFileName(fileName));

        await using var file = File.Create(destination);
        await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return destination;
    }
}
```

`tests/Lego2STL.UiTests/DocumentImportTests.cs`:

```csharp
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.Gui.Services;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// What a picked document becomes before a run can use it.
/// </summary>
public sealed class DocumentImportTests
{
    [Fact]
    public async Task A_picked_document_becomes_a_file_under_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-import-" + Path.GetRandomFileName());
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("ID;Codice Lego\n"));

        var path = await DocumentImport.CopyInAsync(source, "6324712.csv", root);

        path.Should().Be(Path.Combine(root, "imports", "6324712.csv"));
        File.ReadAllText(path).Should().StartWith("ID;Codice Lego");

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task A_picker_that_hands_over_a_path_cannot_escape_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-import-" + Path.GetRandomFileName());
        using var source = new MemoryStream([1, 2, 3]);

        var path = await DocumentImport.CopyInAsync(source, "../../escape.csv", root);

        path.Should().Be(Path.Combine(root, "imports", "escape.csv"));

        Directory.Delete(root, recursive: true);
    }
}
```

- [ ] **Step 5: Give the window a storage root and use the home**

In `UserSettings`, add the root the mobile heads write under — application data on every platform, which `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` already answers correctly on Android and iOS:

```csharp
/// <summary>Where runs go when there is nowhere beside the input to write.</summary>
public static string StorageRoot { get; } =
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lego2STL",
        "runs");
```

In `RunDocumentViewModel.cs:524`, pass a home in, chosen once:

```csharp
var runner = new PipelineRunner(Say, new Watcher(Report), RunHomes.Current);
```

and add `src/Lego2STL.Gui/Services/RunHomes.cs`:

```csharp
using Lego2STL.Core.Run;

namespace Lego2STL.Gui.Services;

/// <summary>Which home this application's runs use: the desktop's, unless a head says otherwise.</summary>
public static class RunHomes
{
    public static IRunHome Current { get; set; } = new BesideTheInputRunHome();
}
```

Set it from each mobile entry point, before `App` starts — in `MainActivity.CustomizeAppBuilder` and `AppDelegate.CustomizeAppBuilder`:

```csharp
RunHomes.Current = new ApplicationStorageRunHome(UserSettings.StorageRoot);
```

- [ ] **Step 6: Run the tests and the builds**

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline, plus the six new tests passing.

Run: `dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Debug -f net10.0-android36.0` and again with `-f net10.0-ios26.0`, then `dotnet restore Lego2STL.slnx`
Expected: PASS.

- [ ] **Step 7: Commit and record**

```bash
git add src/Lego2STL.Core/Run/ApplicationStorageRunHome.cs src/Lego2STL.Gui/Services src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs src/Lego2STL.Gui/Platforms tests PROGRESS.md
git commit -m "feat: a run has a home on a platform with nowhere beside the input to write"
```

> **What the real build corrected (Task 5).** One carry-over from Task 2's discovery: the plan says
> to set `RunHomes.Current` "in `MainActivity.CustomizeAppBuilder` and `AppDelegate.CustomizeAppBuilder`",
> but Task 2 already found that `CustomizeAppBuilder` lives on `MainApplication` on Android (the
> `AvaloniaAndroidApplication<App>` subclass), not on `MainActivity` — `AvaloniaMainActivity` carries
> no such override in this Avalonia version. Set it in `MainApplication.CustomizeAppBuilder` instead;
> the iOS side is exactly as the plan describes, in `AppDelegate.CustomizeAppBuilder`.

---

### Task 6: Results leave by a share sheet

**Files:**
- Create: `src/Lego2STL.Gui/Services/IDesktopActions.cs`
- Modify: `src/Lego2STL.Gui/Services/Desktop.cs` (the two public methods delegate; today's body becomes the default implementation)
- Create: `src/Lego2STL.Gui/Platforms/Android/AndroidShareActions.cs`
- Create: `src/Lego2STL.Gui/Platforms/iOS/AppleShareActions.cs`
- Test: `tests/Lego2STL.UiTests/DesktopActionsTests.cs`

**Interfaces:**
- Consumes: the nine existing call sites in `CataloguePartViewModel` (145, 207, 216, 221), `RunDocumentViewModel` (416, 419, 422, 425) and `SettingsViewModel` (316). None of them change.
- Produces:

```csharp
public interface IDesktopActions
{
    void Open(string path);
    void Reveal(string path);
}

public static class Desktop
{
    public static IDesktopActions Handler { get; set; }
    public static void Open(string path);
    public static void Reveal(string path);
}
```

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using FluentAssertions;
using Lego2STL.Gui.Services;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// The nine buttons that hand a file to the machine, and who they hand it to.
/// </summary>
/// <remarks>
/// A phone has no file manager to reveal a folder in, so revealing has to become sharing.
/// What is checked here is only that the call reaches whatever the platform installed -
/// what Android and iOS then do with it cannot be checked without a device.
/// </remarks>
public sealed class DesktopActionsTests
{
    private sealed class Recorder : IDesktopActions
    {
        public List<string> Opened { get; } = [];
        public List<string> Revealed { get; } = [];

        public void Open(string path) => Opened.Add(path);
        public void Reveal(string path) => Revealed.Add(path);
    }

    [Fact]
    public void Opening_reaches_the_installed_handler()
    {
        var original = Desktop.Handler;
        var recorder = new Recorder();

        try
        {
            Desktop.Handler = recorder;

            Desktop.Open(@"C:\runs\6324712\3mf\black.3mf");
            Desktop.Reveal(@"C:\runs\6324712\stl\3705.stl");

            recorder.Opened.Should().ContainSingle().Which.Should().EndWith("black.3mf");
            recorder.Revealed.Should().ContainSingle().Which.Should().EndWith("3705.stl");
        }
        finally
        {
            Desktop.Handler = original;
        }
    }

    [Fact]
    public void Nothing_is_handed_over_when_there_is_nothing_to_hand()
    {
        var original = Desktop.Handler;
        var recorder = new Recorder();

        try
        {
            Desktop.Handler = recorder;

            Desktop.Open("   ");
            Desktop.Reveal(string.Empty);

            recorder.Opened.Should().BeEmpty();
            recorder.Revealed.Should().BeEmpty();
        }
        finally
        {
            Desktop.Handler = original;
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.UiTests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~DesktopActionsTests`
Expected: FAIL — `IDesktopActions` does not exist.

- [ ] **Step 3: Split `Desktop` into a facade and a default**

`IDesktopActions.cs`:

```csharp
namespace Lego2STL.Gui.Services;

/// <summary>How this platform hands a file or an address to whoever handles such things.</summary>
public interface IDesktopActions
{
    void Open(string path);

    /// <summary>Shows the file where it lives - or, where there is no such place, shares it.</summary>
    void Reveal(string path);
}
```

In `Desktop.cs`, keep the existing `Start`/`Open`/`Reveal` bodies but move them into a nested `private sealed class ProcessStart : IDesktopActions`, and make the static methods delegate, keeping the empty-path guard at the front so it is enforced once:

```csharp
public static IDesktopActions Handler { get; set; } = new ProcessStart();

public static void Open(string path)
{
    if (!string.IsNullOrWhiteSpace(path))
    {
        Handler.Open(path);
    }
}

public static void Reveal(string path)
{
    if (!string.IsNullOrWhiteSpace(path))
    {
        Handler.Reveal(path);
    }
}
```

The `catch` around `Process.Start` moves with the body: a convenience button is still not worth an error dialog.

- [ ] **Step 4: Write the two mobile handlers**

`src/Lego2STL.Gui/Platforms/Android/AndroidShareActions.cs`:

```csharp
using System;
using System.IO;
using Android.Content;
using AndroidX.Core.Content;
using Lego2STL.Gui.Services;
using Application = Android.App.Application;

namespace Lego2STL.Gui.Platforms.Android;

/// <summary>A phone has no file manager to reveal a folder in, so revealing becomes sharing.</summary>
public sealed class AndroidShareActions : IDesktopActions
{
    public void Open(string path)
    {
        try
        {
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute) && !File.Exists(path))
            {
                Start(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(path)));
                return;
            }

            Share(path);
        }
        catch (Exception ex) when (ex is ActivityNotFoundException or IOException)
        {
            // A convenience button is not worth an error dialog, on any platform.
        }
    }

    public void Reveal(string path) => Open(path);

    private static void Share(string path)
    {
        var context = Application.Context;
        var uri = FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", new Java.IO.File(path));

        var intent = new Intent(Intent.ActionSend)
            .SetType("application/octet-stream")
            .PutExtra(Intent.ExtraStream, uri)
            .AddFlags(ActivityFlags.GrantReadUriPermission);

        Start(Intent.CreateChooser(intent, "Lego2STL")!);
    }

    private static void Start(Intent intent) =>
        Application.Context.StartActivity(intent.AddFlags(ActivityFlags.NewTask));
}
```

`src/Lego2STL.Gui/Platforms/iOS/AppleShareActions.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Foundation;
using Lego2STL.Gui.Services;
using UIKit;

namespace Lego2STL.Gui.Platforms.iOS;

/// <summary>The share sheet, which is the only way anything leaves a sandboxed application.</summary>
public sealed class AppleShareActions : IDesktopActions
{
    public void Open(string path)
    {
        if (Uri.IsWellFormedUriString(path, UriKind.Absolute) && !File.Exists(path))
        {
            UIApplication.SharedApplication.OpenUrl(new NSUrl(path), new NSDictionary(), null);
            return;
        }

        if (!File.Exists(path))
        {
            return;
        }

        var controller = new UIActivityViewController([NSUrl.FromFilename(path)], null);
        var root = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)?
            .RootViewController;

        // An iPad presents this from a point rather than full screen, and throws without one.
        if (controller.PopoverPresentationController is { } popover && root is not null)
        {
            popover.SourceView = root.View;
            popover.SourceRect = new CoreGraphics.CGRect(root.View!.Bounds.GetMidX(), root.View.Bounds.GetMidY(), 0, 0);
            popover.PermittedArrowDirections = 0;
        }

        root?.PresentViewController(controller, animated: true, completionHandler: null);
    }

    public void Reveal(string path) => Open(path);
}
```

Install each from its entry point beside the `RunHomes.Current` line added in Task 5:

```csharp
Desktop.Handler = new AndroidShareActions();   // iOS: new AppleShareActions();
```

Android also needs a `FileProvider` entry in `AndroidManifest.xml` and a `Platforms/Android/Resources/xml/file_paths.xml` naming the storage root, or the share sheet receives a URI it cannot read.

- [ ] **Step 5: Run the tests and the builds**

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline, plus the two new tests. The desktop behaviour is unchanged because `ProcessStart` is still the default.

Run: `dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Debug -f net10.0-android36.0` and `-f net10.0-ios26.0`
Expected: PASS.

- [ ] **Step 6: Commit and record**

```bash
git add src/Lego2STL.Gui/Services src/Lego2STL.Gui/Platforms tests/Lego2STL.UiTests/DesktopActionsTests.cs PROGRESS.md
git commit -m "feat: results leave a phone by a share sheet"
```

---

### Task 7: A phone never downloads the whole library

**Files:**
- Modify: `src/Lego2STL.Core/LDraw/EscalatingLDrawLibrary.cs:6-30` (the options record) and `:100-175` (the two escalation points), plus the constructor's pre-open at `:87`
- Modify: `src/Lego2STL.Core/Pipeline/RunSettings.cs:204-210` (`LDrawOptions`) and the flags block near `:140-146`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Modify: `src/Lego2STL.Gui/Platforms/Android/MainActivity.cs`, `src/Lego2STL.Gui/Platforms/iOS/AppDelegate.cs`
- Test: `tests/Lego2STL.Tests/LDraw/CappedAcquisitionTests.cs`

**Interfaces:**
- Consumes: `LDrawSourceOptions`, `EscalatingLDrawLibrary`, `RunSettings.LDrawOptions`, all existing.
- Produces: `LDrawSourceOptions.AllowFullArchive` (`bool`, `init`, default **`true`**); `RunSettings.AllowFullArchive` (`bool`, `init`, default **`true`**) flowing into `LDrawOptions`; `TextKey.MsgLDrawArchiveSkipped`.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;
using Xunit;

namespace Lego2STL.Tests.LDraw;

/// <summary>
/// The escalation, stopped one step short.
/// </summary>
/// <remarks>
/// A phone must not start an 80 MB download over somebody's mobile connection, so the last
/// step of the escalation is switchable. What is checked here is that switching it off
/// changes nothing else: a part that a local directory can answer is still answered, and a
/// part nobody can answer is still reported by name rather than silently dropped.
/// </remarks>
public sealed class CappedAcquisitionTests
{
    [Fact]
    public void The_whole_library_is_allowed_unless_somebody_says_otherwise()
    {
        new LDrawSourceOptions().AllowFullArchive.Should().BeTrue();
        new RunSettings().AllowFullArchive.Should().BeTrue();
        new RunSettings().LDrawOptions.AllowFullArchive.Should().BeTrue();
    }

    [Fact]
    public void Refusing_it_reaches_the_options_a_run_uses()
    {
        new RunSettings { AllowFullArchive = false }.LDrawOptions.AllowFullArchive.Should().BeFalse();
    }

    [Fact]
    public async Task A_capped_library_still_answers_from_a_local_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lego2stl-ldraw-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(directory, "parts"));
        File.WriteAllText(Path.Combine(directory, "parts", "cube.dat"), "0 Cube\n0 BFC CERTIFY CCW\n");

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions { LocalDirectory = directory, AllowFullArchive = false },
            _ => { },
            Strings.For(DisplayLanguages.Fallback));

        (await library.TryReadAsync("cube.dat")).Should().Contain("0 Cube");
        library.Missing.Should().BeEmpty();

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task A_capped_offline_library_reports_what_it_cannot_answer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lego2stl-ldraw-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions { LocalDirectory = directory, Offline = true, AllowFullArchive = false },
            _ => { },
            Strings.For(DisplayLanguages.Fallback));

        (await library.TryReadAsync("3705.dat")).Should().BeNull();
        library.Missing.Should().Contain("3705.dat");

        Directory.Delete(directory, recursive: true);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~CappedAcquisitionTests`
Expected: FAIL — `AllowFullArchive` does not exist.

- [ ] **Step 3: Add the option and the setting**

In `LDrawSourceOptions`, beside `RefusalsBeforeFullDownload`:

```csharp
/// <summary>Whether the escalation may end in the whole 144 MB library; false on a phone.</summary>
public bool AllowFullArchive { get; init; } = true;
```

In `RunSettings`, beside `Offline` and `IncludeUnofficial`:

```csharp
/// <summary>Whether a run may download the whole library, rather than only the files it needs.</summary>
public bool AllowFullArchive { get; init; } = true;
```

and add `AllowFullArchive = AllowFullArchive,` to the `LDrawOptions` initialiser.

- [ ] **Step 4: Gate the three places that reach for the archive**

In `EscalatingLDrawLibrary`: guard the constructor's `ZipLDrawLibrary.Open(archivePath, _words)` pre-open with `if (_options.AllowFullArchive && File.Exists(archivePath))`, and guard both `DownloadCompleteAsync` calls in `TryReadAsync`. Where the per-file library has run out of patience but the archive is not allowed, say so once and carry on recording misses:

```csharp
if (_perFile.RefusalCount >= _options.RefusalsBeforeFullDownload)
{
    _log(_words.Format(TextKey.MsgLDrawRefused, _perFile.RefusalCount));

    if (_options.AllowFullArchive)
    {
        _perFileAbandoned = true;
        await DownloadCompleteAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
        _log(_words[TextKey.MsgLDrawArchiveSkipped]);
    }
}
```

Add `MsgLDrawArchiveSkipped` to `TextKey.cs` beside `MsgLDrawRefused`, with English `"Not downloading the whole library here; only the files this run needs."` and Italian `"La libreria completa non viene scaricata qui; solo i file necessari a questa elaborazione."`

- [ ] **Step 5: Set it false on both phones**

In `MainActivity.CustomizeAppBuilder` and `AppDelegate.CustomizeAppBuilder`, beside the `RunHomes.Current` and `Desktop.Handler` lines from Tasks 5 and 6:

```csharp
RunDefaults.AllowFullArchive = false;
```

Add `src/Lego2STL.Gui/Services/RunDefaults.cs` holding that one static property (default `true`), and apply it where `RunOptionsViewModel.ToSettings()` builds its `RunSettings`, so the option the window shows and the settings a run uses agree.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline, plus four new tests. If a window test fails because of the new `TextKey`, restore the language it switched — do not remove the key.

- [ ] **Step 7: Commit and record**

```bash
git add src/Lego2STL.Core/LDraw/EscalatingLDrawLibrary.cs src/Lego2STL.Core/Pipeline/RunSettings.cs src/Lego2STL.Core/Text src/Lego2STL.Gui tests/Lego2STL.Tests/LDraw/CappedAcquisitionTests.cs PROGRESS.md
git commit -m "feat: a phone fetches only the geometry its own parts list needs"
```

---

### Task 8: A smoke head that runs the whole pipeline on a device

**Files:**
- Create: `tests/Lego2STL.MobileSmokeTest/Lego2STL.MobileSmokeTest.csproj`
- Create: `tests/Lego2STL.MobileSmokeTest/PipelineFixture.cs`
- Create: `tests/Lego2STL.MobileSmokeTest/Platforms/Android/MainActivity.cs`
- Create: `tests/Lego2STL.MobileSmokeTest/Platforms/Android/AndroidManifest.xml`
- Create: `tests/Lego2STL.MobileSmokeTest/Platforms/iOS/Main.cs`, `AppDelegate.cs`, `Info.plist`
- Create: `tests/Lego2STL.MobileSmokeTest/Platforms/MacOS/Program.cs`
- Create: `tests/Lego2STL.MobileSmokeTest/README.md`
- Modify: `Lego2STL.slnx`
- Test: `tests/Lego2STL.Tests/Pipeline/SmokeFixtureTests.cs`

**Interfaces:**
- Consumes: `ApplicationStorageRunHome` (Task 5), `RunSettings.AllowFullArchive` (Task 7), `PipelineRunner`.
- Produces:

```csharp
public sealed record SmokeResult(bool Passed, string Detail);

public static class PipelineFixture
{
    public static async Task<SmokeResult> RunAsync(string root, CancellationToken cancellationToken = default);
}
```

`RunAsync` writes its own parts list and its own one-part LDraw directory under `root`, runs the pipeline, and reports whether a shape file and a plate came out.

- [ ] **Step 1: Write the failing test**

The fixture is testable on the desktop, which is what makes the device run a check of the *platform* rather than of the fixture:

```csharp
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.MobileSmokeTest;
using Xunit;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// The fixture the phones run, exercised where it can be debugged.
/// </summary>
/// <remarks>
/// Everything here is written by the fixture itself: a two-line parts list and a cube in
/// LDraw's own text format, with no subfile references. That is deliberate - it needs no
/// network, no recogniser and no committed document, so a failure on a device is a fact
/// about the device and not about what the device could reach.
/// </remarks>
public sealed class SmokeFixtureTests
{
    [Fact]
    public async Task The_fixture_takes_a_parts_list_all_the_way_to_a_plate()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-smoke-" + Path.GetRandomFileName());

        var result = await PipelineFixture.RunAsync(root);

        result.Passed.Should().BeTrue(result.Detail);
        result.Detail.Should().Contain("cube");

        Directory.Delete(root, recursive: true);
    }
}
```

`Lego2STL.Tests` must reference the smoke project for its `net10.0-windows10.0.19041.0` target only — add `$(MobileTargetFrameworks);net10.0-windows10.0.19041.0` to the smoke project's `TargetFrameworks` so there is something to reference, and keep the platform hosts excluded on that target.

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~SmokeFixtureTests`
Expected: FAIL — `PipelineFixture` does not exist.

- [ ] **Step 3: Write the project**

`tests/Lego2STL.MobileSmokeTest/Lego2STL.MobileSmokeTest.csproj`, copied in shape from `Lego2STL.OcrSmokeTest.csproj` — the same `$(MobileTargetFrameworks)` plus the windows target, the same three `Compile Remove` groups, the same `AndroidManifest`/`SupportedOSPlatformVersion` pair at **24.0**, the same `InfoPlist` line, `ApplicationId` `com.lego2stl.pipelinesmoketest`, and a project reference to `Lego2STL.Core`. Add it to `Lego2STL.slnx`.

- [ ] **Step 4: Write the fixture**

```csharp
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;

namespace Lego2STL.MobileSmokeTest;

/// <summary>What the smoke test found out, in a shape a screen and a log can both carry.</summary>
public sealed record SmokeResult(bool Passed, string Detail);

/// <summary>
/// A whole run, from a parts list to a plate, with nothing outside this file involved.
/// </summary>
public static class PipelineFixture
{
    // A cube in LDraw's own text format: four type-3 lines are enough to be a shape, and no
    // subfile reference means no library and no network.
    private const string Cube = """
        0 Cube
        0 BFC CERTIFY CCW
        3 16 0 0 0 20 0 0 20 0 20
        3 16 0 0 0 20 0 20 0 0 20
        3 16 0 0 0 0 0 20 0 -20 20
        3 16 0 0 0 0 -20 20 20 0 20
        """;

    private const string PartsList = """
        ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita
        1;cube;cube;Black;#05131D;1
        """;

    public static async Task<SmokeResult> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        try
        {
            var home = new ApplicationStorageRunHome(root);
            var library = Path.Combine(root, "ldraw", "parts");
            Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(library, "cube.dat"), Cube);

            var imports = Path.Combine(root, "imports");
            Directory.CreateDirectory(imports);
            var input = Path.Combine(imports, "cube.csv");
            File.WriteAllText(input, PartsList);

            var settings = new RunSettings
            {
                InputPath = input,
                LDrawDirectory = Path.Combine(root, "ldraw"),
                Offline = true,
                AllowFullArchive = false,
            };

            var outcome = await new PipelineRunner(home: home).RunAsync(settings, cancellationToken);

            var layout = home.Plan(settings)!;
            var shapes = Directory.Exists(layout.StlDirectory)
                ? Directory.GetFiles(layout.StlDirectory, "*.stl")
                : [];
            var plates = Directory.Exists(layout.PlateDirectory)
                ? Directory.GetFiles(layout.PlateDirectory, "*.3mf")
                : [];

            return shapes.Length > 0 && plates.Length > 0
                ? new SmokeResult(true, $"cube: {shapes.Length} shape, {plates.Length} plate, in {layout.Root}")
                : new SmokeResult(false, $"cube: {shapes.Length} shapes and {plates.Length} plates in {layout.Root}");
        }
        catch (Exception ex)
        {
            // The smoke test's own failure has to be as legible as a real one.
            return new SmokeResult(false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
```

Adjust the parts-list column wording only if `PartsListCsv` rejects it — the reader recognises the headings in either language, so the English row above is the one to try first, and the run's own error names the column if it does not.

- [ ] **Step 4b: Run the desktop test**

Run: `dotnet test tests/Lego2STL.Tests --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0 --filter FullyQualifiedName~SmokeFixtureTests`
Expected: PASS. Fix the fixture — not the pipeline — until it does.

- [ ] **Step 5: Write the three hosts**

Android `MainActivity`: an `Activity` with `MainLauncher = true` that shows the detail in a `TextView` **and** writes it to the log, because CI reads the log:

```csharp
var result = await PipelineFixture.RunAsync(FilesDir!.AbsolutePath);
var line = (result.Passed ? "SMOKE PASS " : "SMOKE FAIL ") + result.Detail;

Android.Util.Log.Info("Lego2STL", line);
text.Text = line;
```

iOS `AppDelegate`: the same verdict in a full-screen `UILabel` and on `Console.WriteLine`, which `simctl launch --console` carries. macOS `Program.cs`: top-level statements writing the line to stdout and exiting `0` or `1`, so it can be run by hand on a Mac. Manifest, plist and `minSdkVersion` follow the Global Constraints.

- [ ] **Step 6: Write the README**

`tests/Lego2STL.MobileSmokeTest/README.md`, in the shape of the OCR one: what it proves (the pipeline, not the recogniser), what it does not (anything a person can see or touch), the three run commands, and one line saying CI runs this on an emulator and a simulator, unlike the OCR smoke test which no CI job runs.

- [ ] **Step 7: Build every target and commit**

Run: `dotnet build tests/Lego2STL.MobileSmokeTest -c Release -f net10.0-android36.0`, then `-f net10.0-ios26.0`, then `-f net10.0-macos26.0`, then `dotnet restore Lego2STL.slnx && dotnet build Lego2STL.slnx -c Debug`
Expected: PASS for all four.

```bash
git add tests/Lego2STL.MobileSmokeTest tests/Lego2STL.Tests/Pipeline/SmokeFixtureTests.cs Lego2STL.slnx PROGRESS.md
git commit -m "feat: a smoke test that takes a parts list to a plate on a phone"
```

> **What the real build corrected (Task 8).** Three things the plan's fixture snippet did not
> survive contact with the actual pipeline, plus one machine-level surprise:
>
> 1. **The plan's `RunSettings` for the fixture omitted `Kind = InputKind.PartsList`.** Since
>    `Kind` defaults to `InputKind.Document`, the pipeline tried to read the fixture's CSV as a
>    PDF and failed immediately with "Could not find the version header comment". Added the
>    missing `Kind`.
> 2. **The parts list's third column, "Codice BrickLink", is the BrickLink *colour* code — an
>    integer — not a part code**, despite the plan's row putting `cube` there. `PartsListCsv`
>    parses it with `int.TryParse` and throws otherwise. Changed the row's third field to `11`;
>    nothing downstream looks the value up for a fixture part that exists only in this file.
> 3. **`Android.Util.Log.Info(...)` in `MainActivity.cs` doesn't compile** — this file's own
>    namespace, `Lego2STL.MobileSmokeTest.Platforms.Android`, has a segment named `Android` that
>    shadows the global one, the same class of clash Task 2 hit with `Application` and `Button`.
>    Qualified it as `global::Android.Util.Log.Info(...)`.
> 4. **This machine's installed Android SDK (`Microsoft.Android.Sdk.Windows` 36.1.69) developed a
>    reproducible `XAGJS7000` failure** ("Mono.Android.dll ... being used by another process",
>    sometimes a Cecil metadata-write crash instead) inside `GenerateJavaStubs`'s parallel type
>    scan, for *every* Android project in the repo including the pre-existing, previously-working
>    `Lego2STL.OcrSmokeTest` — not something this task's code caused. `dotnet workload repair`
>    itself failed with a fatal MSI error (`0x00000643`), which needs elevation or a reboot this
>    session couldn't provide. The one thing that reliably compiles the Android target on this
>    machine right now is `dotnet build Lego2STL.slnx -c Debug` — the actual Global Constraints
>    build gate — which built all three Android projects cleanly; an isolated
>    `dotnet build ... -c Release -f net10.0-android36.0` on `Lego2STL.MobileSmokeTest` or
>    `Lego2STL.OcrSmokeTest` alone reproduces the failure consistently. Whoever picks this up next:
>    try `dotnet workload repair` again after a reboot, or from an elevated prompt, before assuming
>    the code is at fault.

---

### Task 9: CI builds the applications

**Files:**
- Modify: `.github/workflows/package.yml:68-96` (the `mobile` job)

**Interfaces:**
- Consumes: the four-target Gui from Task 2 and the smoke project from Task 8.
- Produces: an uploaded APK artifact named `android-apk`, and an iOS simulator `.app` built but not signed.

- [ ] **Step 1: Extend the `mobile` job**

After the three `Build Core for ...` steps, add:

```yaml
      - name: Build the window for android
        run: dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f net10.0-android36.0

      - name: Package the APK
        run: dotnet publish src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f net10.0-android36.0 -p:TargetFrameworks=net10.0-android36.0

      - uses: actions/upload-artifact@v4
        with:
          name: android-apk
          path: src/Lego2STL.Gui/bin/Release/net10.0-android36.0/publish/*.apk
          if-no-files-found: error

      # The simulator needs no certificate. A device build does, and there is no Apple
      # Developer Program membership yet, so the signing step below stays switched off
      # until there are secrets to switch it on with.
      - name: Build the window for the iOS simulator
        run: dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f net10.0-ios26.0 -p:RuntimeIdentifier=iossimulator-arm64

      - name: Sign for a device
        if: false
        run: dotnet publish src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f net10.0-ios26.0 -p:TargetFrameworks=net10.0-ios26.0 -p:RuntimeIdentifier=ios-arm64 -p:CodesignKey="${{ secrets.IOS_SIGNING_IDENTITY }}" -p:CodesignProvision="${{ secrets.IOS_PROVISIONING_PROFILE }}"

      - name: Build the pipeline smoke test
        run: |
          dotnet build tests/Lego2STL.MobileSmokeTest -c Release -f net10.0-android36.0
          dotnet build tests/Lego2STL.MobileSmokeTest -c Release -f net10.0-ios26.0 -p:RuntimeIdentifier=iossimulator-arm64
```

- [ ] **Step 2: Check it locally as far as it can be checked**

Run: `act -W .github/workflows/package.yml -l`
Expected: the job list parses, `mobile` present. `act` cannot *run* this job — it needs Xcode and runs Linux containers only — so the first real signal is the push. `packaging/act/run.ps1 -DryRun` does not see this workflow at all.

Run the same commands locally instead, which this machine can do for Android: `dotnet publish src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f net10.0-android36.0 -p:TargetFrameworks=net10.0-android36.0`
Expected: an `.apk` under `bin/Release/net10.0-android36.0/publish/`.

- [ ] **Step 3: Commit and record**

```bash
git add .github/workflows/package.yml PROGRESS.md
git commit -m "ci: build the Android package and the iOS simulator application"
```

---

### Task 10: CI runs it on an emulator and a simulator

**Files:**
- Modify: `.github/workflows/package.yml` (a new `mobile-smoke` job)

**Interfaces:**
- Consumes: the smoke heads from Task 8.
- Produces: a job that fails when either platform does not print `SMOKE PASS`.

- [ ] **Step 1: Add the job**

```yaml
  mobile-smoke:
    name: mobile smoke
    needs: [mobile]
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install the mobile workloads
        run: dotnet workload install android ios

      # One launch and one log line each. Nothing here taps anything, which is what keeps an
      # emulator job worth having: a mis-timed tap is the usual reason they go flaky.
      - name: Android
        uses: reactivecircus/android-emulator-runner@v2
        with:
          api-level: 30
          arch: x86_64
          target: google_apis
          script: |
            dotnet build tests/Lego2STL.MobileSmokeTest -c Release -f net10.0-android36.0 -t:Install
            adb logcat -c
            adb shell am start -n com.lego2stl.pipelinesmoketest/.MainActivity
            timeout 180 adb logcat -s Lego2STL:I -m 1 | tee smoke-android.txt
            grep -q "SMOKE PASS" smoke-android.txt

      - name: iOS
        run: |
          dotnet build tests/Lego2STL.MobileSmokeTest -c Release -f net10.0-ios26.0 -p:RuntimeIdentifier=iossimulator-arm64
          APP=$(find tests/Lego2STL.MobileSmokeTest/bin/Release/net10.0-ios26.0 -name "*.app" -maxdepth 3 | head -1)
          xcrun simctl boot "iPhone 16" || true
          xcrun simctl install booted "$APP"
          xcrun simctl launch --console-pty booted com.lego2stl.pipelinesmoketest | tee smoke-ios.txt
          grep -q "SMOKE PASS" smoke-ios.txt

      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: mobile-smoke-logs
          path: smoke-*.txt
          if-no-files-found: ignore
```

- [ ] **Step 2: Push and read the first real run**

There is no local route: `act` runs Linux containers and has no Xcode, and no emulator exists on this machine. Push the branch and read the job. Expect to correct, in this order: the simulator device name (`xcrun simctl list devices` in a step if `iPhone 16` is not present on the image), the `.app` search path, and the Android activity name (`adb shell cmd package resolve-activity --brief com.lego2stl.pipelinesmoketest`).

If the emulator step proves unreliable across three consecutive runs for reasons unrelated to the code, move this job to a nightly `schedule:` trigger rather than adding retries, and say so in the job comment.

- [ ] **Step 3: Commit and record**

```bash
git add .github/workflows/package.yml PROGRESS.md
git commit -m "ci: run the pipeline smoke test on an emulator and a simulator"
```

---

### Task 11: What a person needs to know

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/plans/2026-09-02-android-and-ios.md` (this file — record what the real builds taught, as Phase D did)
- Modify: `PROGRESS.md`

**Interfaces:**
- Consumes: everything above.
- Produces: no code.

- [ ] **Step 1: Write the README section**

Under the existing platform notes, a short section saying: the window runs on Android and iOS; a run's folder lives in application storage rather than beside the document, because a picked document has no folder to sit beside; results leave by the share sheet; a phone downloads only the geometry its own parts list needs and never the whole library; the iOS build is simulator-only until there is a Developer Program membership. Name what is **not** on a phone: the `bricks` command. Keep each point to a sentence.

- [ ] **Step 2: Record the discoveries in this plan**

Append to each task a short note of anything the real build corrected — the pattern Phase D used, and the reason its record is worth reading. Anything found about Avalonia's mobile backends, the emulator job, or the manifest belongs here rather than in a commit message.

- [ ] **Step 3: Run everything one last time**

Run: `dotnet build Lego2STL.slnx -c Debug`
Run: `dotnet test --configuration Release --nologo -p:TargetFrameworks=net10.0-windows10.0.19041.0`
Expected: the baseline, and no new failure.

- [ ] **Step 4: Close the phase**

```bash
git add README.md docs/superpowers/plans/2026-09-02-android-and-ios.md PROGRESS.md
git commit -m "docs: what the mobile applications do, and what they deliberately do not"
```

Append `PHASE:E WAVE:11 STATUS:complete` and then `PHASE:E WAVE:0 STATUS:complete`, both with the same timestamp, as every earlier lot has.

---

## Notes for whoever executes this

- **Task 2 is the risky one.** If Avalonia's Android or iOS backend refuses something the four screens use, fix that control — do not fork the layout. The whole point of Task 1 is that there is one layout.
- **Do not "fix" the baseline failures.** `CalibrationPlateTests.With_nothing_to_build_from_it_says_so_and_writes_no_shapes` fails on unmodified `main`, and the contention flakes are a known, separate problem.
- **The order matters twice.** Task 1 before Task 2, because the single-view lifetime needs a view to show. Tasks 5 and 7 before Task 8, because the smoke fixture uses both.
- **What no CI job can prove** is whether the application is usable in the hand: the collapsed rail, the document picker, where the share sheet puts a plate. That is a person's, on a real device, and the README should not imply otherwise.
