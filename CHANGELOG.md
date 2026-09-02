# Changelog

Every notable change, by version. Follows [Keep a Changelog](https://keepachangelog.com/) and
[Semantic Versioning](https://semver.org/). See the versioning rule in [CLAUDE.md](CLAUDE.md).

## [Unreleased]

### Added

- CI also runs on every push or merge to `main`, not only on a version tag or a manual run.

### Fixed

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
