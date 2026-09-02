# Pipeline smoke test

Proves the whole pipeline - a parts list to a plate - runs on a phone, under the real
`ApplicationStorageRunHome` and the real capped LDraw source. It does not prove anything a
person can see or touch: not the collapsed sidebar, not the document picker, not where the
share sheet lands a file. It carries its own typeset fixture (a two-line parts list and a
cube written in LDraw's own text format), so it needs no network, no recogniser and no
committed document - a failure here is a fact about the device, not about what the device
could reach.

Unlike the OCR smoke test, CI runs this one: the `mobile-smoke` job in
`.github/workflows/package.yml` launches it on a booted Android emulator and a booted iOS
simulator and fails the build if either does not print `SMOKE PASS`.

| Platform | Run | Read the result |
|---|---|---|
| Android | Deploy `Lego2STL.MobileSmokeTest` to an emulator or device from Visual Studio / `dotnet build -t:Run -f net10.0-android36.0` | On screen, and in `adb logcat` under the tag `Lego2STL` |
| iOS | Run in the Simulator from Visual Studio for Mac / Xcode, or `dotnet build -t:Run -f net10.0-ios26.0` | On screen, and on stdout |
| macOS | `dotnet run --project tests/Lego2STL.MobileSmokeTest -f net10.0-macos26.0` | On stdout |

A PASS names the cube's shape and plate counts and where they were written; a FAIL names what
went wrong, including an exception, since the smoke test's own failure has to be as legible as
a real one.
