# Packaging

Building the thing people install, for Windows, Linux and macOS.

## What comes out

| System | File | What it is |
|---|---|---|
| Windows | `Lego2STL-<version>-win-x64.exe` | The installer. Installs .NET 10 first if the machine has not got it, then Lego2STL for the current user — no administrator unless the runtime is missing. Start Menu entry, and `lego2stl` on the path. |
| Windows | `Lego2STL-<version>-win-x64.zip` | The same folder, to unpack anywhere. Expects .NET 10 to be there. |
| Linux | `Lego2STL-<version>-linux-x64.run` | The installer. `./…run` for you alone under `~/.local`; `sudo ./…run --system` for everyone. Fetches .NET 10 into `~/.dotnet` when there is none. |
| Linux | `Lego2STL-<version>-linux-x64.tar.gz` | The same folder plus `install.sh`. Expects .NET 10 to be there. |
| macOS | `Lego2STL-<version>-osx-universal.pkg` | The installer, for any Mac. Installs .NET 10 into `/usr/local/share/dotnet` when there is none. |
| macOS | `Lego2STL-<version>-osx-universal.zip` | The same application bundle. |

## Building

```
# Windows
./packaging/build-windows.ps1 -Version 1.2.3

# Linux and macOS
./packaging/build-unix.sh linux x64       1.2.3
./packaging/build-unix.sh macos universal 1.2.3
```

Everything lands in `artifacts/dist`. `-Version`/the trailing argument here is for building one
package by hand. The real workflow, `packaging/local-windows.ps1` and `packaging/act/run.*`
never pass one — they read it from `<Version>` on `src/Lego2STL.Core/Lego2STL.Core.csproj` via
`packaging/version.sh`, so a package built through any of those always carries the version the
code itself claims. See [README-act.md](../README-act.md#versioning) and
[CLAUDE.md](../CLAUDE.md).

The Windows installer needs the WiX toolset, which is a .NET tool, and three extensions:

```
dotnet tool install --global wix --version 6.0.1
wix extension add --global WixToolset.UI.wixext/6.0.1
wix extension add --global WixToolset.Netfx.wixext/6.0.1
wix extension add --global WixToolset.BootstrapperApplications.wixext/6.0.1
```

Without the toolset the script still produces the zip and says why there is no installer.

`.github/workflows/package.yml` builds all of them, each on its own kind of machine, which is
the only way to get all three: the Windows installer needs the WiX toolset, and the macOS one
needs `lipo`, `pkgbuild` and `productbuild`, which ship only with macOS.

`packaging/act/` runs the Linux part of that workflow on this machine, in Docker, without
GitHub, and `packaging/local-windows.ps1` does the Windows part directly. See
[README-act.md](../README-act.md).

## .NET is a dependency, not a passenger

Every package used to carry its own copy of .NET, twice over — once per program. That made the
Windows installer 152 MB. Now the two programs share one folder of 67 MB, 24 MB packed, and
the runtime is something the installer puts on the machine if it is not already there.

The runtime is pinned in `runtime.json`: a version, and per platform a file name, a SHA512 and
a byte count. Downloads come from
`https://builds.dotnet.microsoft.com/dotnet/Runtime/<version>/<file>`, which never changes
under a given version, so a pinned fingerprint stays true. Every installer checks that
fingerprint before unpacking anything, and refuses otherwise.

Bumping it is deliberate:

```
./packaging/refresh-runtime.sh          # the newest 10.0.x
./packaging/refresh-runtime.sh 10.0.11  # a particular one
```

Nothing downloads a script and runs it. Microsoft publish `dotnet-install.sh` for this and it
is deliberately not used: it is a moving target fetched at install time, and all it does here
is unpack a tarball, which the installers do against a fingerprint they were built with.

## How each system is asked whether it has .NET

Not through the registry, on any of them. Two things were measured on a machine with .NET
10.0.10 installed: the `sharedhost` version read `8.0.29`, and the `sharedfx` key that most
installers search did not exist. Runtime patches also install side by side, so there is no one
upgrade family to look for.

- **Windows** asks the host resolver, through WiX's `netfx:DotNetCoreSearch`.
- **Linux and macOS** look in the places a runtime is kept — `$DOTNET_ROOT`, `~/.dotnet`,
  `/usr/share/dotnet`, `/usr/lib/dotnet`, `/usr/local/share/dotnet` — and then ask
  `dotnet --list-runtimes` if there is a `dotnet` on the path.

Any `10.0.x` counts. The programs ask for 10.0.0 and the host picks the newest patch it has.

## Nothing is a single file any more

It was, and the change is deliberate. A framework-dependent single file cannot be compressed —
the SDK refuses, `NETSDK1176` — and came out at 66.9 MB against the 57 MB compressed
self-contained one it would have replaced, while carrying some 60 MB of shared assemblies
twice, once per program. One shared folder is 67.4 MB for both programs and 24.4 MB packed.

## The macOS package is one file for both kinds of Mac

The programs are published twice, for Intel and for Apple silicon, and fused: files that are
identical — the assemblies, which are most of it — are carried once, and the few genuinely
native ones are combined with `lipo`. Every fused binary is checked for both architectures,
and a build that cannot produce one fails rather than shipping a package that runs on half the
Macs.

A file that differs between the two builds and is *not* a program is an error rather than a
guess: it means something in the build is not reproducible, and quietly picking one of the two
would ship whichever the loop happened to reach first.

## The macOS package cannot be built anywhere else

The programs cross-build fine — `dotnet publish -r osx-arm64` on Windows or Linux emits a
genuine `Mach-O 64-bit arm64 executable`, and the bundle is only a folder with an `Info.plist`
in it. What cannot be done off a Mac is turn that into something a Mac will open:

| Needed for | Tool | Ships with |
|---|---|---|
| Combining the Intel and the Apple silicon binaries into one | `lipo` | macOS |
| Ad-hoc signature, without which Apple silicon rejects a copied bundle outright | `codesign` | macOS |
| An archive that keeps the permission bits and the signature | `ditto` | macOS |
| The installer itself | `pkgbuild`, `productbuild` | macOS |

The executable bit is the quiet one. Most non-Unix filesystems do not carry it, so a bundle
zipped elsewhere can arrive unrunnable even when everything else about it is right.

Running `build-unix.sh macos` off a Mac therefore says so and exits non-zero before building
anything, rather than leaving a folder that resembles a package. To actually get one:

- **A Mac.** `./packaging/build-unix.sh macos universal 1.2.3` does the whole thing.
- **The workflow's `macos` job**, which builds the universal package on a real runner. One
  job rather than two, because one file serves every Mac — and GitHub bills macOS minutes at
  ten times the Linux rate.
- **A hosted Mac**, if neither is to hand. Virtualising macOS is only licensed on Apple
  hardware, so a VM on this machine is not an option.

Splitting the work is possible if it ever matters — cross-publish the binaries anywhere, and
let a Mac do only the signing and the packaging — but nothing here does that today.

## Two programs, not one

Every package holds two:

| | Windows | Linux | Inside the Mac bundle |
|---|---|---|---|
| Console | `lego2stl.exe` | `lego2stl` | `lego2stl` |
| Window | `Lego2STL.Gui.exe` | `lego2stl-gui` | `Lego2STL.Gui` |

They are separate because one executable cannot be both on Windows: a console program flashes
a window when it starts from a shortcut, and a windowed one cannot write to a pipe. Both drive
exactly the same code.

**Their names must differ by more than capitalisation.** Windows file names are
case-insensitive, and so is a Mac disk by default, so `Lego2STL.exe` beside `lego2stl.exe` is
one file: the package silently ships the same program twice and the Start Menu entry opens a
console. Both build scripts check for both programs by name after publishing, and stop if
either is missing.

Each project also publishes into a folder of its own before the two are gathered into one.
Publishing both into the same folder does not work — the second run clears what the first put
there.

## What each system can do

Reading a parts catalogue out of a document needs text recognition, and the recogniser used is
part of Windows. So:

- **Windows** does everything.
- **Linux and macOS** do everything after the parts list — shapes, plates, clearance,
  calibration — and can also start from a set number. Asked to read a document, they say so
  and point at what does work.

The Windows package is built against the Windows target framework for that reason; the others
are built against the plain one, which has no recogniser compiled into it at all.

## Signing

Nothing here is signed with a real identity, because that needs certificates that belong to
whoever is publishing.

- **Windows.** An unsigned installer raises SmartScreen on first run. Sign the `.exe` with
  `signtool` if you have a code-signing certificate.
- **macOS.** The script applies an ad-hoc signature when `codesign` is present. That is enough
  for a copied bundle not to be rejected outright on Apple silicon, but it is not notarisation:
  the first open still needs right-click → Open. Real distribution needs a Developer ID and a
  trip through `notarytool`.
- **Linux.** Nothing to sign. The `.run` is a shell script with a tarball on the end of it,
  and what it fetches is checked against a fingerprint built into it.
