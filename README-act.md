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

That builds the Linux `.run` installer and the tarball, installs what it built both as a
machine that has .NET and as one that has none, runs the packaging tests, and leaves the
packages under `.act-artifacts`.

---

## Versioning

No script here takes a version as a parameter, and neither does the real workflow. The one
place a version is set is `<Version>` on `src/Lego2STL.Core/Lego2STL.Core.csproj`;
`packaging/version.sh` reads it, and everything else — the `version` job, the local one, the
Windows script — calls that rather than holding a copy. See [CLAUDE.md](CLAUDE.md) for the
rule that keeps it current, and [CHANGELOG.md](CHANGELOG.md) for what each version carried.

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

act runs Linux containers, and Windows and macOS runners are not containers at all — a limit
of the approach, not something left undone. The Windows job has a way round it; the macOS one
does not.

| Job | Locally | Why |
|---|---|---|
| `version` | **yes** | plain shell |
| `linux` | **yes** | the container is Ubuntu; it builds the `.run` and the tarball, installs both, and runs the packaging tests |
| `windows` | **yes, but not through act** | `./packaging/local-windows.ps1` — see below |
| `test` | no | runs on Windows, because reading a document needs the recogniser that is part of Windows |
| `macos` | no | needs macOS for `lipo`, `codesign`, `pkgbuild` and `productbuild` |
| `release` | no | publishes to GitHub, which is not a thing to do from a laptop by accident |

For the test job, run the suite directly instead — it is the same one the workflow runs:

```powershell
dotnet test -c Release
```

There is no local route to a macOS package at all. `lipo`, `codesign`, `ditto`, `pkgbuild`
and `productbuild` ship only with macOS, and no container provides them; see the macOS section
of [packaging/README.md](packaging/README.md).

## Windows, without act

Since there is nothing for act to run the `windows` job in, one script does it directly:

```powershell
./packaging/local-windows.ps1
```

That does what the `windows` job does, step for step, against the same scripts: the tests, the
build, and then a look inside the installer to confirm the .NET runtime is fetched when needed
rather than carried, and that its fingerprint is the one pinned in `runtime.json`. Pass
`-SkipTests` to skip the suite, which the workflow runs in a job of its own anyway. Like
everything else here, its version comes from `Lego2STL.Core`'s `<Version>` element, not from a
parameter.

It needs the WiX toolset and its three extensions — see
[packaging/README.md](packaging/README.md) — and Git for Windows, whose `bash` is what runs
the version rule the workflow shares.

## What the Linux run now proves

More than it used to. The container has .NET installed, so simply installing the `.run` there
says nothing about the case that matters. The job therefore installs it twice: once normally,
and once with `dotnet` taken out of the environment and the runtime search pointed at an empty
directory, so the installer genuinely finds nothing, downloads the pinned runtime, checks its
fingerprint, unpacks it, and runs the program against it. That path does not exist on any
GitHub runner, because every runner already has .NET.

---

## Options

```powershell
./packaging/act/run.ps1                        # both jobs
./packaging/act/run.ps1 -Job version           # just the version job, which takes seconds
./packaging/act/run.ps1 -Job linux             # just the build
./packaging/act/run.ps1 -DryRun                # list what would run, start nothing
./packaging/act/run.ps1 -SkipActVersionCheck   # run on an act with the CVEs anyway
```

```bash
./packaging/act/run.sh                         # both jobs
./packaging/act/run.sh version                 # one job
./packaging/act/run.sh "" --dryrun             # anything after the job is passed to act
SKIP_ACT_VERSION_CHECK=1 ./packaging/act/run.sh
```

Neither script takes a version. Both print the one `Lego2STL.Core` carries before building, so
a stale `<Version>` is obvious before ten minutes are spent on it — see
[Versioning](#versioning) below.

### Running act directly

The scripts are a convenience, not a wrapper you are stuck with. The equivalent by hand:

```bash
act workflow_dispatch -W packaging/act/local-package.yml
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
| `packaging/act/run.ps1` | the runner, for PowerShell |
| `packaging/act/run.sh` | the runner, for a shell |
| `packaging/local-windows.ps1` | the Windows job, run directly, because act cannot host it |
| `packaging/lib/find-git-bash.ps1` | finds a `bash` that can read a Windows path, for the two PowerShell scripts |
| `packaging/version.sh` | shared with the real workflow: reads the version off `Lego2STL.Core.csproj` |
| `packaging/build-unix.sh` | shared with the real workflow: builds the actual package |
| `packaging/tests/*.test.sh` | the pin, the runtime probe, and the built installer |

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

Expect, at whatever version `Lego2STL.Core` currently carries, about 15 MB each:

```
Lego2STL-0.2.0-linux-x64.run
Lego2STL-0.2.0-linux-x64.tar.gz
```

The linux job also installs both, prints the tarball's file list, and runs the three packaging
test scripts, so a run says what it built and that it works rather than only that it built.

---

## When it goes wrong

**`Cannot connect to the Docker daemon`** — Docker Desktop is not running, or is in Windows
container mode. Right-click the tray icon, *Switch to Linux containers*.

**A step fails on the first run but not the second** — usually the image pull or the SDK
download timing out. Run it again before reading anything into it.

**`unable to get git repo`** — act wants a git repository. Run it from the repository root;
both scripts change there themselves.

**`Permission denied` running one of the packaging scripts** — act copies the working tree in
with `docker cp`, and a Windows filesystem has no executable bit to copy. The local workflow
has a step that restores it; a hand-run `act` needs `chmod +x` first. A real runner clones
from git, which does carry the bit, so this never happens there.

**The packages come out numbered differently than expected** — the version is read off
`<Version>` on `src/Lego2STL.Core/Lego2STL.Core.csproj`, not passed in. Check that element,
not the command line.

**A step fails with exit code 141 having printed everything it was going to** — that is
`SIGPIPE`, from something piped into a reader that stops early, turned into a failure by
`set -o pipefail`. The step did its work; the pipe is what failed.

**`actions/upload-artifact` fails** — it needs somewhere to write. `.actrc` passes
`--artifact-server-path`; if act is being run by hand without it, that is the reason.

**The run is slow every time, not just the first** — `--rm` in `.actrc` removes the container
at the end, but the image stays. If the image is being pulled repeatedly, check that
`docker images` actually lists `catthehacker/ubuntu:act-latest`.
