# Changelog

Every notable change, by version. Follows [Keep a Changelog](https://keepachangelog.com/) and
[Semantic Versioning](https://semver.org/). See the versioning rule in [CLAUDE.md](CLAUDE.md).

## [Unreleased]

### Added

- CI also runs on every push or merge to `main`, not only on a version tag or a manual run.

### Fixed

- The Android smoke test in CI now runs on an Intel macOS runner, where the emulator can
  actually use hardware acceleration; the ARM-based default runner cannot boot it at all. The
  Android and iOS smoke tests are now separate CI jobs, each installing only the workload it
  needs, and each fails within 20 minutes instead of running for hours if it genuinely hangs.
- The version-lookup step in CI no longer fails with "Permission denied".
- The Windows test step in CI no longer fails to build; it now runs only the two real test
  projects instead of the whole solution.
- A saved tolerance set in the window's settings screen no longer leaks into other tests run in
  the same job, which used to make the Windows test step fail intermittently.
- Choosing Italian in the window and leaving it, in a test, no longer leaves the window's saved
  language stuck in Italian for tests that open a fresh window afterwards.
- The Android build and packaging steps in CI no longer fail with a missing `System.Runtime`
  assembly.
- The iOS simulator build steps in CI no longer refuse to run without trimming enabled.
- The Linux and macOS packaging steps in CI no longer fail with "Permission denied".
- The iOS build steps in CI no longer fail over an Xcode version newer than the .NET for iOS
  workload supports.
- The iOS simulator build steps in CI no longer hang for over an hour: they now build with
  the interpreter, which a simulator supports directly, instead of the ahead-of-time compiler a
  real device needs.
- The macOS packaging step no longer refuses to fuse the universal binary over files that
  differ between the two publishes for reasons other than architecture. **Known limitation:**
  the Intel copy of each such file is kept as-is, unverified so far on real Apple silicon
  hardware — see `README-GAPS.md`.
- The Android emulator step in CI gets more time to boot before giving up.

## [0.2.0] - 2026-09-02

### Added

- Android and iOS applications, built on the same window and view models as desktop.
- CI packages the Android APK and an iOS simulator build, and smoke-tests both on a booted
  emulator and simulator.
- The package version is now read from `Lego2STL.Core`'s `<Version>` element instead of being
  passed in as a tag or a workflow input.

### Changed

- A run's folder lives in the app's own storage rather than beside the input document, on
  platforms where a picked document has no folder to sit beside.
- Results leave a phone through the OS share sheet.
- A phone downloads only the LDraw geometry its own parts list needs, never the whole library.

## [0.1.0] - 2026-08-23

### Added

- Initial desktop window and command line: extract a catalogue from a document, build a parts
  list or a set number into printable plates, calibrate a printer's clearance, and browse the
  colour cross-reference across BrickLink, LEGO, Rebrickable and LDraw.
- Windows, Linux and macOS packaging, with self-contained installers that fetch .NET 10 only
  when the machine has none.
