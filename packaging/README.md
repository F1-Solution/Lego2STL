# Packaging

Building the thing people install, for Windows, Linux and macOS.

## What comes out

| System | File | What it is |
|---|---|---|
| Windows | `Lego2STL-<version>-win-x64.msi` | An installer. Per-user by default, so it needs no administrator. Puts both programs in place, adds a Start Menu entry, and puts `lego2stl` on the path. |
| Windows | `Lego2STL-<version>-win-x64.zip` | The same two programs, to unpack anywhere. Always produced. |
| Linux | `Lego2STL-<version>-linux-<arch>.tar.gz` | The programs, a menu entry, and `install.sh`, which copies them into `~/.local` or, with `--system`, `/usr/local`. |
| Linux | `lego2stl_<version>_<arch>.deb` | For anything Debian-derived. Only when `dpkg-deb` is available, which in practice means building on Linux. |
| macOS | `Lego2STL-<version>-osx-<arch>.dmg` | A disk image to drag into Applications. Only when building on macOS. |
| macOS | `Lego2STL-<version>-osx-<arch>.zip` | The same application bundle. |

## Building

```
# Windows
./packaging/build-windows.ps1 -Version 1.2.3

# Linux and macOS
./packaging/build-unix.sh linux x64   1.2.3
./packaging/build-unix.sh macos arm64 1.2.3
```

Everything lands in `artifacts/dist`.

The Windows installer needs the WiX toolset, which is a .NET tool:

```
dotnet tool install --global wix --version 6.0.1
```

Without it the script still produces the zip and says why there is no installer.

`.github/workflows/package.yml` builds all of them, each on its own kind of machine, which is
the only way to get all three: an installer needs the Windows toolset, a `.deb` needs Debian's,
and a disk image needs macOS.

`packaging/act/` runs the Linux part of that workflow on this machine, in Docker, without
GitHub. See [packaging/act/README.md](act/README.md).

## The macOS package cannot be built anywhere else

The programs cross-build fine — `dotnet publish -r osx-arm64` on Windows or Linux emits a
genuine `Mach-O 64-bit arm64 executable`, and the bundle is only a folder with an `Info.plist`
in it. What cannot be done off a Mac is turn that into something a Mac will open:

| Needed for | Tool | Ships with |
|---|---|---|
| Ad-hoc signature, without which Apple silicon rejects a copied bundle outright | `codesign` | macOS |
| An archive that keeps the permission bits and the signature | `ditto` | macOS |
| The disk image | `hdiutil` | macOS |

The executable bit is the quiet one. Most non-Unix filesystems do not carry it, so a bundle
zipped elsewhere can arrive unrunnable even when everything else about it is right.

Running `build-unix.sh macos` off a Mac therefore says so, and exits non-zero rather than
leaving a folder that resembles a package. To actually get one:

- **A Mac.** `./packaging/build-unix.sh macos arm64 1.2.3` does the whole thing.
- **The workflow's `macos` job**, which already builds both architectures on a real runner.
  Note that GitHub bills macOS minutes at ten times the Linux rate.
- **A hosted Mac**, if neither is to hand. Virtualising macOS is only licensed on Apple
  hardware, so a VM on this machine is not an option.

Splitting the work is possible if it ever matters — cross-publish the binaries anywhere, and
let a Mac do only the signing and the image — but nothing here does that today.

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

Each project also publishes into a folder of its own before they are gathered. Publishing two
into one folder does not work — the second run clears what the first put there.

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

- **Windows.** An unsigned installer raises SmartScreen on first run. Sign the `.msi` with
  `signtool` if you have a code-signing certificate.
- **macOS.** The script applies an ad-hoc signature when `codesign` is present. That is enough
  for a copied bundle not to be rejected outright on Apple silicon, but it is not notarisation:
  the first open still needs right-click → Open. Real distribution needs a Developer ID and a
  trip through `notarytool`.
- **Linux.** Nothing to sign. A `.deb` can be signed with `dpkg-sig` if a repository needs it.
