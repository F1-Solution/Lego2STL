# Running the packaging workflow locally

GitHub Actions is not the only way to run the packaging workflow. [act](https://github.com/nektos/act)
runs it in Docker on this machine, which is useful when the org's Actions minutes are
unavailable, and quicker than pushing a tag to find out whether something works.

```powershell
./packaging/act/run.ps1
```

```bash
./packaging/act/run.sh
```

That builds the Linux tarball and the `.deb`, prints what is inside each, and leaves them
under `.act-artifacts`.

## What runs, and what cannot

act runs Linux containers. Windows and macOS runners are not containers at all, so four of
the six jobs are out of reach locally no matter what — this is a limit of the approach, not
something left undone.

| Job | Locally | Why |
|---|---|---|
| `version` | **yes** | plain shell |
| `linux` | **yes** | the image is Ubuntu and carries `dpkg-deb`, so the `.deb` is built too |
| `test` | no | runs on Windows, because reading a document needs the recogniser that is part of Windows |
| `windows` | no | needs the Windows installer toolset |
| `macos` | no | needs macOS to make a disk image |
| `release` | no | publishes to GitHub, which is not a thing to do from a laptop by accident |

For the one that matters most, run it directly instead — it is the same suite the workflow
runs:

```powershell
dotnet test -c Release
```

## Why there are two workflow files

`.github/workflows/package.yml` is the real one. `packaging/act/local-package.yml` is the
subset act can run.

They are not two copies of the build. Both are thin wrappers around `packaging/version.sh`
and `packaging/build-unix.sh`, which hold everything that actually decides what a package
contains. So a green run here says something about the real workflow rather than only about
itself.

The local one lives outside `.github/workflows` deliberately: GitHub would otherwise list it
as a workflow of its own, and it is not for GitHub. act is pointed at it with `-W`.

The one thing that can still drift is the .NET version each asks for, so both runner scripts
compare them and say so.

## Before the first run

- **act must be 0.2.86 or later.** Earlier versions carry CVE-2026-34041 and CVE-2026-34042,
  which act reports on every run of its own accord. The scripts refuse to start on an
  affected version, because this hands a container a copy of the repository.

  ```powershell
  winget upgrade nektos.act
  ```

  To override anyway: `-SkipActVersionCheck`, or `SKIP_ACT_VERSION_CHECK=1` for the shell one.
- **Docker Desktop must be running**, in Linux container mode. The scripts check, and say so
  plainly rather than failing somewhere further in.
- The first run pulls about **1.1 GB** of image (`catthehacker/ubuntu:act-latest`) and then
  downloads the .NET SDK inside it. Allow ten to fifteen minutes. Later runs reuse the image
  and are much quicker.

## Options

```powershell
./packaging/act/run.ps1 -Version 1.2.0     # stamp a particular version
./packaging/act/run.ps1 -Job version       # just the version job, which takes seconds
./packaging/act/run.ps1 -DryRun            # list what would run, start nothing
```

```bash
./packaging/act/run.sh 1.2.0
./packaging/act/run.sh 1.2.0 version
./packaging/act/run.sh 0.0.0-local "" --dryrun
```

A version is refused here for the same reasons the workflow refuses it, before ten minutes
are spent finding out: `1.2.0` and `1.2.0-rc1` are versions, `v1.2` and `main` are not.

## Where things end up

| Path | What |
|---|---|
| `.act-artifacts/` | what `upload-artifact` collected, the same files a release would carry |
| `artifacts/dist/` | only when run with `--bind`; otherwise the build stays inside the container |

Both are ignored by git.

By default act copies the repository into the container, so a run cannot disturb the working
tree. Adding `--bind` mounts it instead, and the packages then appear in `artifacts/dist` on
this machine as well — convenient, at the cost of letting the container write here.

## When it goes wrong

**`Cannot connect to the Docker daemon`** — Docker Desktop is not running, or is in Windows
container mode. Right-click the tray icon, *Switch to Linux containers*.

**A step fails on the first run but not the second** — usually the image pull or the SDK
download timing out. Run it again before reading anything into it.

**`unable to get git repo`** — act wants a git repository. Run it from the repository root;
both scripts change there themselves.

**The `.deb` is missing and a warning says `dpkg-deb` was not found** — the image was
overridden with one that is not Debian-derived. The mapping in `.actrc` is what to check.
