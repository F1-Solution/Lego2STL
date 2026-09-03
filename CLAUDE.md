# Project rules

## Versioning

`Lego2STL.Core`'s `<Version>` element (`src/Lego2STL.Core/Lego2STL.Core.csproj`) is the single
place a version is set. Nothing else carries one in: not a git tag, not a CI input. Every other
place that needs the version — the packaging workflow, the local act runner, the Windows
build script — reads it from there through `packaging/version.sh`. See
[README-act.md](README-act.md#versioning).

**Every commit that changes code must:**

1. **Bump `<Version>`**, following [Semantic Versioning](https://semver.org/):
   - **MAJOR** — an incompatible change: a command, a file format, or a public interface breaks.
   - **MINOR** — a backwards-compatible feature: something new works that did not before.
   - **PATCH** — a backwards-compatible fix: something that was wrong now behaves correctly,
     with nothing new added.
   - While the project is pre-1.0, a breaking change may still land as a MINOR bump — see
     SemVer §4.
2. **Add a line to [CHANGELOG.md](CHANGELOG.md)**, under an `[Unreleased]` heading (or a new
   version heading, dated, if this commit is the one that closes it out) — one line, in the
   `Added` / `Changed` / `Fixed` / `Removed` section it belongs to, describing the change from
   a user's or integrator's point of view, not the internals. [Keep a Changelog](https://keepachangelog.com/)
   is the format.

A change that touches only documentation, tests, or CI configuration with no effect on what
ships still gets a changelog line if a person would notice it (a new CI check, a renamed
script); a pure typo fix or comment edit does not need one.

**Every push to `main` publishes a release**, tagged `vX.Y.Z` from `<Version>` at that commit,
by the `release` job itself — nothing is tagged by hand. Because of that, `<Version>` must be
unique across *commits* on `main`, not just across product changes: the `version` job refuses
to build at all when `vX.Y.Z` already exists, so every commit pushed to `main` needs its own
bump, with no exception for CI-only or documentation-only changes — there is simply no such
thing as a commit on `main` that does not produce a release. This is also what stops the second
workflow run that pushing the tag itself fires straight back at this workflow (`refs/tags/v*` is
still a trigger): by then the tag it would need already exists, so it fails at the same check
instead of building and releasing all over again.
