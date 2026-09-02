# Local verification gaps

Things this plan's tasks call for that could not be verified on this development
machine, and why. Each entry names the task, what the plan asked for, what actually
happened, and where the real signal comes from instead.

## Task 9 — Android Release publish

**Plan asked for:** `dotnet publish src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release -f
net10.0-android36.0 -p:TargetFrameworks=net10.0-android36.0` run locally, expecting an
`.apk` under `bin/Release/net10.0-android36.0/publish/`.

**What happened:** fails deterministically (reproduced on a clean rebuild, not flaky)
with `XAGJS7000` — `GenerateJavaStubs`'s parallel Java-type scan across the four default
Android RIDs races to read the shared `Mono.Android.dll` pack file. Debug config builds
fine; Release fails even without publishing, so it's Release's marshal-method rewriting
that trips the race, not `publish` specifically. This is the same machine-level SDK issue
Task 8 already found on `Microsoft.Android.Sdk.Windows` 36.1.69 — not a defect in this
plan's code.

**Real signal instead:** `dotnet build Lego2STL.slnx -c Debug` (the Global Constraints
build gate) built the Android target cleanly, and CI's `mobile` job (`macos-latest`) is
the actual verification for the Release/publish path — it doesn't share this machine's
SDK install. Full detail recorded in
`docs/superpowers/plans/2026-09-02-android-and-ios.md`, Task 9.

## Task 10 — the emulator/simulator smoke run

**Plan asked for:** a `mobile-smoke` CI job that boots an Android emulator and an iOS
simulator, launches the smoke app on each, and greps its log for `SMOKE PASS`.

**What happened:** no local verification route exists on this machine at all.
- `act` runs the job's steps inside Linux containers; it has no Xcode, so the iOS half
  can never run there, and `act -l` can only confirm the job *parses*, not that it works.
- There is no Android emulator (AVD) installed or startable on this machine, so the
  Android half has nothing to boot against either.
- `packaging/act/run.ps1 -DryRun` doesn't see this workflow at all.

**Real signal instead:** push and read the first real CI run on `macos-latest`, per the
plan's own Task 10 Step 2. Expect to correct the simulator device name, the `.app` search
path, and the Android activity name against what the runner actually reports, in that
order.

## macOS universal binary — non-reproducible files between the two publishes

**What happened:** the first real `macos` CI run reached `fuse-universal.sh` and failed:

```
fused ./libpdfium.dylib                        x86_64 arm64
./Lego2STL.Gui.deps.json differs between the two builds and is not a program.
Something in the build is not reproducible; refusing to pick one.
```

The script's assumption — that everything but the native `.dylib`/launcher binaries is
byte-identical between an `osx-x64` and an `osx-arm64` publish of the same project — is
wrong for `deps.json`: a framework-dependent, RID-specific publish embeds its own RID in
the file (as part of the runtime target name), so the two payloads' copies never match by
design. This was never caught before because `packaging/lib/payload.sh`'s missing execute
bit always failed the job earlier, until that was fixed.

Fixing that one revealed a second, less explicable case at the same check:
`Lego2STL.Gui.dll` — the managed assembly itself — also differs between the two publishes.
There is no `RuntimeIdentifier`-conditional code anywhere in the source, so this is not a
real logic difference; the leading theory was an obj-directory path embedded in the PE
debug directory (each RID publishes into its own `obj/Release/net10.0/<rid>/`), which
`-p:ContinuousIntegrationBuild=true` in `payload.sh` is supposed to prevent. Adding it did
not change the result, so the actual cause is still unknown.

**What was done:** rather than special-case each file as one is found, `fuse-universal.sh`
now treats any non-Mach-O difference the same way: log it loudly as a known limitation and
keep the Intel (`osx-x64`) copy, rather than failing the build.

**Not verified:** whether the fused payload actually runs correctly on real Apple silicon
hardware. The universal `.dylib`s and the two program launchers are confirmed fused
(`lipo -archs` reports both architectures on each), but everything else in the payload —
`deps.json` included — is the Intel build's copy, unverified on arm64. If a Mac running
this build behaves oddly on Apple silicon, start here:
- `deps.json`'s RID-specific `runtimeTargets` section maps a native asset to the
  architecture that should load it, and was written for `osx-x64` only; the fix likely
  needs to merge both files' `runtimeTargets` sections rather than pick one.
- Whatever makes `Lego2STL.Gui.dll` differ is still unknown; diffing the two publishes'
  intermediate `obj` output (not just the final `.dll`) on an actual Mac would be the next
  step, since that is exactly what this session could not do.

**Real signal instead:** install and run the packaged app on both an Intel Mac and an
Apple silicon Mac.
