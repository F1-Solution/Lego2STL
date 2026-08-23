# Running the packaging workflow locally

GitHub Actions is not the only way to run the packaging workflow.
[act](https://github.com/nektos/act) runs it in Docker on this machine, which is useful when
the org's Actions minutes are unavailable, and quicker than pushing a tag to find out whether
something works.

```powershell
./packaging/act/run.ps1
```

```bash
./packaging/act/run.sh
```

That builds the Linux tarball and the `.deb`, prints what is inside each, and leaves them
under `.act-artifacts`.

---

## Before the first run

### act must be 0.2.86 or later

Earlier versions carry **CVE-2026-34041** and **CVE-2026-34042**, which act reports on every
run of its own accord. The runner scripts refuse to start on an affected version, because
this hands a container a copy of the repository.

```powershell
winget upgrade nektos.act
```

To override anyway: `-SkipActVersionCheck`, or `SKIP_ACT_VERSION_CHECK=1` for the shell one.

### Docker Desktop must be running, in Linux container mode

The scripts check both, and say so plainly rather than failing somewhere further in. If it is
in Windows container mode, right-click the tray icon and choose *Switch to Linux containers*.

### Allow time for the first run

It pulls about **1.1 GB** of image (`catthehacker/ubuntu:act-latest`) and then downloads the
.NET SDK inside it. Ten to fifteen minutes is normal. Later runs reuse the image and are much
quicker.

If act is not installed at all: `winget install nektos.act`.

---

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

There is no local route to a macOS package. `codesign`, `ditto` and `hdiutil` ship only with
macOS, and no container provides them; see the macOS section of
[packaging/README.md](packaging/README.md).

---

## Options

```powershell
./packaging/act/run.ps1                        # both jobs, version 0.0.0-local
./packaging/act/run.ps1 -Version 1.2.0         # stamp a particular version
./packaging/act/run.ps1 -Job version           # just the version job, which takes seconds
./packaging/act/run.ps1 -Job linux             # just the build
./packaging/act/run.ps1 -DryRun                # list what would run, start nothing
./packaging/act/run.ps1 -SkipActVersionCheck   # run on an act with the CVEs anyway
```

```bash
./packaging/act/run.sh                         # both jobs, version 0.0.0-local
./packaging/act/run.sh 1.2.0                   # stamp a particular version
./packaging/act/run.sh 1.2.0 version           # one job
./packaging/act/run.sh 0.0.0-local "" --dryrun # anything after the job is passed to act
SKIP_ACT_VERSION_CHECK=1 ./packaging/act/run.sh
```

A version is refused here for the same reasons the workflow refuses it, before ten minutes
are spent finding out: `1.2.0` and `1.2.0-rc1` are versions, `v1.2` and `main` are not. A
hyphen marks a pre-release.

### Running act directly

The scripts are a convenience, not a wrapper you are stuck with. The equivalent by hand:

```bash
act workflow_dispatch \
  -W packaging/act/local-package.yml \
  -e packaging/act/event.json \
  --input version=1.2.0
```

`.actrc` supplies the image mappings, the artifact path and `--rm`, so those need not be
repeated. `act -l -W packaging/act/local-package.yml` lists the jobs without starting
anything.

---

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

---

## The files involved

| Path | What it is |
|---|---|
| `README-act.md` | this |
| `.actrc` | default flags, so the command line stays short |
| `packaging/act/local-package.yml` | the subset of the workflow act can run |
| `packaging/act/event.json` | the `workflow_dispatch` payload, carrying the default version |
| `packaging/act/run.ps1` | the runner, for PowerShell |
| `packaging/act/run.sh` | the runner, for a shell |
| `packaging/version.sh` | shared with the real workflow: turns a tag into a version |
| `packaging/build-unix.sh` | shared with the real workflow: builds the actual package |

---

## Where things end up

| Path | What |
|---|---|
| `.act-artifacts/` | what `upload-artifact` collected, the same files a release would carry |
| `artifacts/dist/` | only when run with `--bind`; otherwise the build stays inside the container |

Both are ignored by git.

By default act copies the repository into the container, so a run cannot disturb the working
tree. Adding `--bind` mounts it instead, and the packages then appear in `artifacts/dist` on
this machine as well — convenient, at the cost of letting the container write here.

Expect, at version `0.0.0-local`:

```
Lego2STL-0.0.0-local-linux-x64.tar.gz
lego2stl_0.0.0-local_amd64.deb
```

The linux job also prints `dpkg-deb --info` and `--contents` for the `.deb`, and the tarball's
file list, so a run says what it built rather than only that it built.

---

## When it goes wrong

**`Cannot connect to the Docker daemon`** — Docker Desktop is not running, or is in Windows
container mode. Right-click the tray icon, *Switch to Linux containers*.

**A step fails on the first run but not the second** — usually the image pull or the SDK
download timing out. Run it again before reading anything into it.

**`unable to get git repo`** — act wants a git repository. Run it from the repository root;
both scripts change there themselves.

**The `.deb` is missing and a warning says `dpkg-deb` was not found** — the image was
overridden with one that is not Debian-derived. The mapping in `.actrc` is what to check.

**`actions/upload-artifact` fails** — it needs somewhere to write. `.actrc` passes
`--artifact-server-path`; if act is being run by hand without it, that is the reason.

**The run is slow every time, not just the first** — `--rm` in `.actrc` removes the container
at the end, but the image stays. If the image is being pulled repeatedly, check that
`docker images` actually lists `catthehacker/ubuntu:act-latest`.
