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
