# Light installers: .NET 10 as a dependency, not a passenger

**Date:** 2026-08-25
**Status:** approved design, not yet implemented

## The problem

Every package ships its own copy of .NET. The Windows installer is 152 MB, the Linux
tarball 82 MB, and each program inside them is a 50-odd MB self-contained single file. Two
programs per package means the runtime is carried twice.

The goal is three light installers — one per system — that install .NET 10 as a dependency
and only when it is missing.

## Measured facts this design rests on

Taken on this machine before designing, because three plausible assumptions turned out to be
wrong:

| Checked | Result |
|---|---|
| `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost\Version` | Reads **8.0.29** while .NET **10.0.10** is installed. Stale. Unusable for detection. |
| `...\x64\sharedfx\Microsoft.NETCore.App` | **Does not exist.** The key most installers search is gone. |
| .NET runtime MSI upgrade family | Patches install **side by side** — 10.0.8 and 10.0.10 are separate products with separate directories. No single UpgradeCode spans the 10.x band. |
| Framework-dependent single file, GUI, win-x64 | **66.9 MB** — *larger* than today's compressed self-contained 57 MB. |
| `EnableCompressionInSingleFile` without self-contained | Refused: `NETSDK1176`. Compression is self-contained-only. |
| Both programs, framework-dependent, one shared folder | **67.4 MB** across 65 files, **24.4 MB** compressed. |
| `builds.dotnet.microsoft.com/dotnet/Runtime/<v>/...` for win-x64, linux-x64, osx-x64, osx-arm64 | All four present, 30–37 MB each. |
| `release-metadata/10.0/releases.json` | Present, 993 KB, carries immutable URL **and** 128-char SHA512 per file. Latest is 10.0.11. |

Two consequences follow directly and are not negotiable within this design:

- **Detection cannot use the registry.** It must ask hostfxr (Windows) or `dotnet
  --list-runtimes` and the known install directories (Unix).
- **Single-file publishing goes away.** It is bigger, cannot be compressed, and duplicates
  ~60 MB of shared DLLs across the two programs.

## What comes out

| System | File | Size | Replaces |
|---|---|---|---|
| Windows | `Lego2STL-<v>-win-x64.exe` | ~26 MB, measured | `.msi` (152 MB) |
| Windows | `Lego2STL-<v>-win-x64.zip` | 24.4 MB, measured | `.zip` (96 MB) |
| Linux | `Lego2STL-<v>-linux-x64.run` | ~16 MB, measured | `.deb` (dropped) |
| Linux | `Lego2STL-<v>-linux-x64.tar.gz` | 16 MB, measured | `.tar.gz` (82 MB) |
| macOS | `Lego2STL-<v>-osx-universal.pkg` | ~35 MB, estimated | `.dmg`, per-arch (dropped) |
| macOS | `Lego2STL-<v>-osx-universal.zip` | ~35 MB, estimated | per-arch `.zip` |

Only macOS is still an estimate; the plan records the real number once a runner has built one.
The Linux payload came out *smaller* than the Windows one despite carrying the same programs —
39 MB against 67.4 MB, 62 files against 65 — because `Microsoft.Windows.SDK.NET.dll`, 23.7 MB
and a third of the Windows payload, exists only on the Windows target. macOS will give some of
that back: a universal build carries two copies of every native binary, though the managed
assemblies, which are the bulk, are carried once.

The installer is what changes per system. The archive beside it is the same payload with
nothing done to it, for people who want a folder rather than an installer.

## Components

### 1. Build model

`Directory.Build.props`, in the `RuntimeIdentifier != ''` group:

- `SelfContained` → `false`
- `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`,
  `EnableCompressionInSingleFile` → removed
- `PublishTrimmed=false` stays. Avalonia loads XAML by reflection.

Each project still publishes into a folder of its own, then the two are **merged** into one
payload folder. Merging rather than publishing both into one place is what the existing
scripts already do, and for the same reason: the second publish clears the first.

The merge asserts, as the current scripts do, that both programs are present and that their
names differ by more than capitalisation.

Roll-forward stays at the default. A `net10.0` app asks for 10.0.0 and the host picks the
newest 10.0.x present, so **any** 10.0.x runtime satisfies it. Detection therefore only has
to answer "is some .NET 10 here", never "which patch".

### 2. `packaging/runtime.json` — one source of truth

```json
{
  "version": "10.0.11",
  "platforms": {
    "win-x64":   { "file": "dotnet-runtime-10.0.11-win-x64.exe",     "sha512": "..." },
    "linux-x64": { "file": "dotnet-runtime-10.0.11-linux-x64.tar.gz", "sha512": "..." },
    "osx-x64":   { "file": "dotnet-runtime-10.0.11-osx-x64.tar.gz",   "sha512": "..." },
    "osx-arm64": { "file": "dotnet-runtime-10.0.11-osx-arm64.tar.gz", "sha512": "..." }
  }
}
```

URLs are derived, not stored: `https://builds.dotnet.microsoft.com/dotnet/Runtime/<version>/<file>`.
Those URLs are immutable, so a pinned hash stays valid forever.

`packaging/refresh-runtime.sh` regenerates the file from
`release-metadata/10.0/releases.json`, which carries both URL and SHA512, so bumping the
runtime downloads under a megabyte rather than 140 MB. Bumping it is a deliberate commit.

Nothing downloads a script and runs it. Microsoft's `dotnet-install.sh` is deliberately not
used: it is a moving target fetched at install time, and everything it does here is "download
this tarball and unpack it", which the installers do against a pinned hash instead.

### 3. Windows: a WiX Burn bundle

`packaging/windows/Bundle.wxs`, built alongside the existing `Lego2STL.wxs`.

Chain, in order:

1. **The runtime.** An `ExePackage` for `dotnet-runtime-<v>-win-x64.exe`.
   - `DetectCondition` from `netfx:DotNetCoreSearch` with `RuntimeType="core"`,
     `Platform="x64"`, `MajorVersion="10"`. That search asks hostfxr, which is the only
     thing on the machine that reliably knows.
   - `Compressed="no"` with a `DownloadUrl` and a `RemotePayload` generated at build time by
     `wix burn remotepayload`. The bundle therefore stays ~25 MB and fetches the runtime only
     when the detect condition says it is absent.
   - `Permanent="yes"`. Uninstalling Lego2STL must not remove a runtime other things use.
   - `PerMachine="no"`. See *Elevation* below.
2. **The application.** The existing per-user MSI, `Vital="yes"`.

`Lego2STL.wxs` keeps its `UpgradeCode`, per-user scope, Start Menu entry and `PATH` entry, so
an existing installation upgrades in place. Its two hand-written `File` elements are replaced
by a single WiX 4+ `<Files Include="<payload>\**" />`, because the payload is now 65 files.

**Elevation.** The requirement is: no prompt when the runtime is present, one prompt when it
is not. Burn registers itself per-machine — and so elevates unconditionally — if any package
in the chain is per-machine. Declaring the runtime `ExePackage` as `PerMachine="no"` keeps
Burn's registration in HKCU and stops Burn elevating; Microsoft's runtime installer then
raises its own UAC prompt from its own manifest at the moment it actually runs.

This is the one uncertain part of the design. It is verified first, before anything is built
on it (see *Verification*). If Burn elevates anyway, the fallback is a single UAC prompt on
every install, and that is a decision to bring back to the user rather than to take quietly.

### 4. Linux: a self-extracting `.run`

`packaging/linux/installer-header.sh` plus the payload tarball appended, assembled by
`build-unix.sh`. Extraction is `tail -n +<N> | tar xz` into a temporary directory — no
`makeself` dependency, because the mechanism is six lines.

```
./Lego2STL-1.2.3-linux-x64.run              # into ~/.local, no root
sudo ./Lego2STL-1.2.3-linux-x64.run --system # into /usr/local
        --prefix <dir>   somewhere else
        --no-runtime     never touch .NET, fail if it is missing
        --uninstall      remove what was installed
        --help
```

Runtime probe, in order: `$DOTNET_ROOT`, `dotnet` on `PATH` (via `dotnet --list-runtimes`),
`~/.dotnet`, `/usr/share/dotnet`, `/usr/lib/dotnet`. A line matching
`^Microsoft.NETCore.App 10\.` counts as found.

Not found, and `--no-runtime` was not passed: download the pinned tarball, verify SHA512,
unpack into `~/.dotnet` (or `/usr/share/dotnet` under `--system`). Verification failure is
fatal and removes the download.

Layout installed:

```
<prefix>/lib/lego2stl/…                     the payload folder
<prefix>/bin/lego2stl                       wrapper script
<prefix>/bin/lego2stl-gui                   wrapper script
<prefix>/share/applications/lego2stl.desktop
```

The wrappers exist because a private runtime under `~/.dotnet` is not on the host's search
path: each sets `DOTNET_ROOT` when the installer provided the runtime, and simply `exec`s the
program when the system already had one.

`packaging/linux/install.sh`, which ships inside the tarball, gains the same runtime probe so
the archive path is not the one that fails confusingly.

**The `.deb` is dropped.** `Depends: dotnet-runtime-10.0` resolves only where
`packages.microsoft.com` or a distro package for 10.0 is already configured, and fails the
install outright otherwise. A dependency that usually cannot be satisfied is worse than no
package.

### 5. macOS: one universal `.pkg`

Publish framework-dependent twice, `osx-x64` and `osx-arm64`, then fuse:

- Every file present in both and **byte-identical** — the managed assemblies, which are the
  bulk — is copied once.
- Every file that differs and is Mach-O — the two apphosts, `libSkiaSharp`, `pdfium`,
  HarfBuzz — is `lipo -create`d.
- Anything that differs and is *not* Mach-O is an error, not a guess.
- Each fused binary is checked with `lipo -archs`, and a result that is not both
  architectures fails the build.

Payload: `/Applications/Lego2STL.app` with the fused folder in `Contents/MacOS`, plus a
`/usr/local/bin/lego2stl` symlink so the console program is on the path.

`preinstall` runs the same probe as Linux and, when nothing is found, installs the runtime for
`uname -m` into `/usr/local/share/dotnet`. That path specifically: it is the documented
default, so the apphost finds it with no environment variable — which matters because an app
launched from Finder inherits no shell environment.

Built with `pkgbuild --root --scripts` then `productbuild --distribution`. The bundle keeps
its ad-hoc `codesign` as today. Nothing is notarised, so a first open still needs
right-click → Open; that is unchanged and already documented.

The arch matrix collapses to a single job, which halves billed macOS minutes.

### 6. CI

`.github/workflows/package.yml`:

- `version`, `test` — unchanged.
- `windows` — adds the `Netfx` and `Bal` WiX extensions to the existing `UI` one; builds the
  bundle and the zip.
- `linux` — builds the `.run` and the tarball.
- `macos` — **no matrix**; one job producing the universal `.pkg` and `.zip`.
- `release` — gathers six files plus `SHA256SUMS.txt`, and its notes gain a line saying the
  installers need .NET 10 and fetch it when it is missing.

### 7. Running it locally

**Linux, under act** — as today, via `packaging/act/run.ps1` / `run.sh`. The local workflow
gains a step that goes further than the real one: it *executes* the `.run` inside the
container with `dotnet` removed from the environment, then runs `lego2stl --version`. That
exercises the download-verify-unpack-and-launch path end to end, which is the part most worth
proving and the part CI cannot show (its runners always have a runtime).

**Windows, natively** — `packaging/local-windows.ps1`, mirroring the windows job step for
step: check for the SDK and `wix`, validate the version string through `version.sh`, build,
then inspect the result with `wix burn` to prove the runtime is chained as a remote payload
rather than embedded. Not act: act runs Linux containers, and a Windows runner is not a
container. `README-act.md` says so plainly rather than implying a gap.

### 8. Guard against silent regression

Each build script asserts a size ceiling on its installer — 40 MB for Windows and Linux, 70 MB
for macOS. A return to self-contained publishing, by an edited property or a stray flag, then
fails the build instead of shipping a 152 MB "light" installer.

## Error handling

| Situation | Behaviour |
|---|---|
| Runtime download fails | Installer stops, says the URL and that a manual .NET 10 install then a re-run will work. Nothing is half-installed. |
| SHA512 mismatch | Fatal. The download is deleted and the mismatch reported. Never proceeds. |
| `--no-runtime` and no runtime found | Refuses, naming what to install. |
| WiX extension missing | Windows script warns and still produces the zip, as it does today for the MSI. |
| `lipo` produces a non-universal binary | Fails the macOS build. No half-universal package is emitted. |
| Installer exceeds its size ceiling | Fails the build. |
| Not macOS, building macOS | Unchanged: says so and exits non-zero rather than leaving a folder that resembles a package. |

## Verification

In order, because the later items build on the earlier ones:

1. **Burn elevation.** Build the bundle, install on a machine with .NET 10 present, and
   confirm no UAC prompt. Then with the runtime absent, confirm exactly one prompt and a
   working install. This gates the Windows design; if it fails, stop and ask.
2. **`netfx:DotNetCoreSearch` exists in WiX 6.0.1.** A build that references it either
   compiles or does not. Checked before the bundle is fleshed out.
3. **Linux, runtime absent.** The act job above, with `dotnet` stripped from the environment.
4. **Linux, runtime present.** The same job unstripped: nothing is downloaded, and
   `~/.dotnet` is not created.
5. **Upgrade over an existing install.** Today's MSI installed, then the new bundle: one
   entry in Programs and Features, not two.
6. **macOS.** Only on the runner. The build's own `lipo -archs` assertions are the check;
   there is no Mac here and virtualising macOS is not licensed on this hardware.

Unit tests are untouched — this is packaging, and none of it is reachable from the test
suite. The verification above is the test.

## Documentation to update

- `README.md` — install instructions, and that .NET 10 is fetched when missing.
- `packaging/README.md` — the output table, the dropped formats, the runtime pin, `lipo`.
- `README-act.md` — the Windows section and why it is a script rather than act.

## Deliberately not done

- **Trimming** to shed `Microsoft.Windows.SDK.NET.dll` (23.7 MB of the 67 MB). Avalonia loads
  XAML by reflection and trimming breaks it.
- **Signing and notarisation.** Needs certificates belonging to whoever publishes.
- **arm64 Linux and arm64 Windows.** No demand recorded, and each adds a build.
- **Bundling the runtime as a fallback** for machines with no network. Would undo the point of
  the change.
